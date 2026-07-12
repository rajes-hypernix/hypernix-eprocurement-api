using System.Reflection;
using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Authorization;
using eProcure.Application.Onboarding;
using eProcure.Application.Procurement;
using eProcure.Application.Sourcing;
using eProcure.Application.Suppliers;
using eProcure.Application.Views;
using eProcure.Domain;
using eProcure.Domain.Views;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

/// <summary>
/// The saved-views engine (D3). The executor DECORATES the same scoped service list
/// methods the screens use (Step 0(c), ruled) — it never composes a fresh query, so
/// vendor scoping, masking and derivations are inherited by construction. Filtering is
/// in-memory over the scoped DTO rows (the ruled trade-off); D4's aggregation seam is
/// the documented extension point for typed IQueryable sources — nothing built here.
/// </summary>
public sealed class SavedViewService(
    AppDbContext db,
    IClock clock,
    ICodeGenerator codes,
    ICurrentUser user,
    IRequisitionService requisitions,
    IRfqService rfqs,
    IPoService pos,
    IInvoiceService invoices,
    IDeliveryService deliveries,
    IVendorService vendors,
    IOnboardingService onboarding) : ISavedViewService
{
    // ---------- visibility + CRUD ----------

    public async Task<IReadOnlyList<SavedViewDto>> ListVisibleAsync(string? recordType, CancellationToken ct = default)
    {
        var q = db.SavedViews.AsNoTracking().Include(v => v.Filters).Include(v => v.Columns).AsQueryable();
        if (recordType is not null) q = q.Where(v => v.RecordType == ParseRecordType(recordType));
        var me = user.UserId;
        var views = await q.Where(v => v.IsSystem || v.IsShared || v.OwnerUserId == me).ToListAsync(ct);
        return views
            .OrderByDescending(v => v.IsSystem).ThenByDescending(v => v.IsShared).ThenBy(v => v.Name)
            .Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<ViewFieldDto>> FieldsAsync(string recordType, CancellationToken ct = default)
    {
        var type = ParseRecordType(recordType);
        var rows = await db.FieldRegistry.AsNoTracking()
            .Where(f => f.RecordType == type).OrderBy(f => f.FieldKey).ToListAsync(ct);
        return rows.Select(f => new ViewFieldDto(
            f.FieldKey, f.Label, f.DataType.ToString(),
            ViewVocabulary.EnumOptions.TryGetValue((type, f.FieldKey), out var opts) ? opts : null)).ToList();
    }

    public async Task<SavedViewDto> CreateAsync(SaveViewRequest req, CancellationToken ct = default)
    {
        var type = ParseRecordType(req.RecordType);
        RequireViewAction(type);                                   // creating a view you cannot run is a 403 now, not a surprise later
        await ValidateDefinitionAsync(type, req, ct);

        var view = new SavedView
        {
            Code = await codes.NextAsync("VIEW", ct),
            Name = req.Name.Trim(),
            RecordType = type,
            OwnerUserId = user.UserId,
            IsShared = false,                                      // sharing is publication — its own gated endpoint
            IsSystem = false,
            CreatedUtc = clock.UtcNow,
            UpdatedUtc = clock.UtcNow,
        };
        ApplyDefinition(view, req);
        db.SavedViews.Add(view);
        await db.SaveChangesAsync(ct);
        return ToDto(view);
    }

    public async Task<SavedViewDto> UpdateAsync(Guid id, SaveViewRequest req, CancellationToken ct = default)
    {
        var view = await LoadOwnedAsync(id, ct);
        var type = ParseRecordType(req.RecordType);
        if (type != view.RecordType)
            throw new ViewValidationException("A view's record type cannot change.");
        await ValidateDefinitionAsync(type, req, ct);

        view.Name = req.Name.Trim();
        // Restrict FKs: children replaced explicitly. The replacements carry preset Guid PKs, so
        // they MUST be AddRange()d — children merely discovered on a tracked parent's nav are
        // classified Modified (an UPDATE of a nonexistent row → spurious 409 on every edit; the
        // D4 dashboard work surfaced this latent D3 edit-path bug, now pinned by a test).
        db.RemoveRange(view.Filters.ToList());
        db.RemoveRange(view.Columns.ToList());
        view.Filters = [];
        view.Columns = [];
        ApplyDefinition(view, req);
        db.AddRange(view.Filters);
        db.AddRange(view.Columns);
        view.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(view);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var view = await LoadOwnedAsync(id, ct);
        db.RemoveRange(view.Filters);
        db.RemoveRange(view.Columns);
        db.SavedViews.Remove(view);
        await db.SaveChangesAsync(ct);
    }

    public async Task<SavedViewDto> ShareAsync(Guid id, bool isShared, CancellationToken ct = default)
    {
        var view = await LoadOwnedAsync(id, ct);                   // publication is the owner's act (role gate is the endpoint's)
        view.IsShared = isShared;
        view.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(view);
    }

    /// <summary>Owner-only write path. An invisible view is a 404 (existence hiding between
    /// principals); a visible-but-not-owned one is a 403; system views are read-only.</summary>
    private async Task<SavedView> LoadOwnedAsync(Guid id, CancellationToken ct)
    {
        var view = await db.SavedViews.Include(v => v.Filters).Include(v => v.Columns)
            .FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException($"View {id} not found.");
        if (view.IsSystem)
            throw new DomainRuleException("System views are read-only.");
        if (view.OwnerUserId != user.UserId)
        {
            if (view.IsShared) throw new ForbiddenException("Only the owner may modify a view.");
            throw new NotFoundException($"View {id} not found.");  // private + foreign → hide existence
        }
        return view;
    }

    // ---------- the run ----------

    /// <summary>The shared run pipeline: visibility → View* check → registry validation →
    /// scoped source → filters. Run shapes rows; aggregate/series (D4) fold them.</summary>
    private async Task<(SavedView View, IReadOnlyDictionary<string, FieldRegistryEntry> Registry, List<object> Rows)>
        PrepareAsync(Guid id, CancellationToken ct)
    {
        var view = await db.SavedViews.AsNoTracking().Include(v => v.Filters).Include(v => v.Columns)
            .FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException($"View {id} not found.");
        var me = user.UserId;
        if (!view.IsSystem && !view.IsShared && view.OwnerUserId != me)
            throw new NotFoundException($"View {id} not found.");  // hide other users' private views

        RequireViewAction(view.RecordType);                        // the ruled dynamic check on top of [Action(UseSavedViews)]

        var registry = await RegistryFor(view.RecordType, ct);
        foreach (var key in view.Filters.Select(f => f.FieldKey).Concat(view.Columns.Select(c => c.FieldKey)).Distinct())
            if (!registry.ContainsKey(key))
                throw new ViewValidationException($"View '{view.Name}' references unknown field '{key}' — the registry has no such {view.RecordType} field.");

        var rows = await SourceRowsAsync(view.RecordType, ct);     // THE scoped sources — see class doc
        var filtered = ApplyFilters(rows.Cast<object>().ToList(), view.Filters, registry);
        return (view, registry, filtered);
    }

    public async Task<ViewRunResult> RunAsync(Guid id, CancellationToken ct = default)
    {
        var (view, registry, filtered) = await PrepareAsync(id, ct);
        filtered = ApplySort(filtered, view.Columns, registry);

        var columns = view.Columns.OrderBy(c => c.Sort)
            .Select(c => new ViewRunColumn(c.FieldKey, c.Label ?? registry[c.FieldKey].Label, registry[c.FieldKey].DataType.ToString()))
            .ToList();

        var shaped = filtered.Select(row =>
        {
            var d = new Dictionary<string, object?> { ["Id"] = Prop(row, "Id") };
            foreach (var c in columns) d[c.FieldKey] = Prop(row, c.FieldKey);
            return d;
        }).ToList();

        return new ViewRunResult(view.Id, view.Name, view.RecordType.ToString(), columns, shaped);
    }

    // ---- D4 aggregation seam: same pipeline, folded instead of shaped. Null semantics as
    // ruled: count→0 honest; sum over zero rows→0; avg over zero rows→null; sum/avg where
    // every input value is null (backfill-null)→null with ExcludedNullCount surfaced.
    // In-memory over the scoped DTO lists (the ruled trade-off); the escape hatch, if real
    // data ever proves strain, is a per-type IScopedQuerySource<TDto> exposing an IQueryable
    // with the SAME scoping predicate — the switch in SourceRowsAsync is the one seam.

    public async Task<Application.Dashboards.ViewAggregateResult> AggregateAsync(Guid id, string fn, string? fieldKey, CancellationToken ct = default)
    {
        var (view, registry, rows) = await PrepareAsync(id, ct);
        fn = fn.ToLowerInvariant();
        if (fn == "count")
            return new(view.Id, fn, null, rows.Count, 0);

        var entry = RequireNumericField(fn, fieldKey, registry);
        var values = rows.Select(r => Prop(r, entry.FieldKey)).ToList();
        var excluded = values.Count(v => v is null);
        var nums = values.Where(v => v is not null).Select(v => ToDecimal(v!)).ToList();

        decimal? value = fn switch
        {
            "sum" => rows.Count > 0 && nums.Count == 0 ? null : nums.Sum(),   // all-null inputs → honest null; zero rows → 0
            "avg" => nums.Count == 0 ? null : Math.Round(nums.Average(), 2),
            _ => throw new ViewValidationException($"Unknown aggregate fn '{fn}' — count|sum|avg."),
        };
        return new(view.Id, fn, entry.FieldKey, value, excluded);
    }

    public async Task<Application.Dashboards.ViewSeriesResult> SeriesAsync(Guid id, string fn, string? fieldKey, string bucketField, int months, CancellationToken ct = default)
    {
        var (view, registry, rows) = await PrepareAsync(id, ct);
        fn = fn.ToLowerInvariant();
        if (!registry.TryGetValue(bucketField, out var bucket) ||
            bucket.DataType is not (FieldDataType.Date or FieldDataType.Instant))
            throw new ViewValidationException($"Series bucket field must be a Date/Instant registry key; '{bucketField}' is not.");
        FieldRegistryEntry? entry = fn == "count" ? null : RequireNumericField(fn, fieldKey, registry);

        months = Math.Clamp(months, 3, 36);
        var now = clock.UtcNow;
        var first = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(months - 1));
        var keys = Enumerable.Range(0, months).Select(i => first.AddMonths(i).ToString("yyyy-MM")).ToList();

        string? BucketOf(object row) => Prop(row, bucketField) switch
        {
            DateOnly d => $"{d.Year:d4}-{d.Month:d2}",
            DateTime t => t.ToString("yyyy-MM"),
            _ => null,                                             // null bucket field → excluded, surfaced
        };

        var unbucketed = rows.Count(r => BucketOf(r) is null);
        var groups = rows.Select(r => (Key: BucketOf(r), Row: r)).Where(x => x.Key is not null)
            .GroupBy(x => x.Key!).ToDictionary(g => g.Key, g => g.Select(x => x.Row).ToList());

        decimal ValueOf(List<object>? bucketRows)
        {
            if (bucketRows is null || bucketRows.Count == 0) return 0m;
            if (fn == "count") return bucketRows.Count;
            var nums = bucketRows.Select(r => Prop(r, entry!.FieldKey)).Where(v => v is not null).Select(v => ToDecimal(v!)).ToList();
            return nums.Count == 0 ? 0m : fn == "sum" ? nums.Sum() : Math.Round(nums.Average(), 2);
        }

        var buckets = keys.Select(k => new Application.Dashboards.SeriesBucketDto(k, ValueOf(groups.GetValueOrDefault(k)))).ToList();
        return new(view.Id, fn, entry?.FieldKey, bucketField, buckets, unbucketed);
    }

    private static FieldRegistryEntry RequireNumericField(string fn, string? fieldKey, IReadOnlyDictionary<string, FieldRegistryEntry> registry)
    {
        if (fieldKey is null || !registry.TryGetValue(fieldKey, out var entry))
            throw new ViewValidationException($"fn={fn} needs a registry fieldKey.");
        if (entry.DataType is not (FieldDataType.Money or FieldDataType.Number))
            throw new ViewValidationException($"fn={fn} needs a Money/Number field; '{fieldKey}' is {entry.DataType}.");
        return entry;
    }

    /// <summary>The (c) table: record type → the existing scoped list the executor builds on.</summary>
    private async Task<System.Collections.IList> SourceRowsAsync(RecordType type, CancellationToken ct) => type switch
    {
        RecordType.Requisition => (System.Collections.IList)await requisitions.ListAsync(ct),          // internal-only via catalog (A7)
        RecordType.Rfq => (System.Collections.IList)await rfqs.ListAsync(ct),                          // live-invitation vendor scoping (A8)
        RecordType.PurchaseOrder => (System.Collections.IList)await pos.ListAsync(ct),                 // VendorId scoping (A12)
        RecordType.Invoice => (System.Collections.IList)await invoices.ListAsync(ct),                  // VendorId scoping (A14)
        RecordType.Asn => (System.Collections.IList)await deliveries.ListAsync(ct),                    // VendorId scoping (A13)
        RecordType.Vendor => (System.Collections.IList)await vendors.ListAsync(new VendorFilter(null, null, null), ct), // list DTO carries no bank fields (A16)
        RecordType.Onboarding => (System.Collections.IList)await onboarding.ListApplicationsAsync(ct), // internal-only via catalog (A20)
        _ => throw new ViewValidationException($"Unknown record type {type}."),
    };

    // ---------- filtering ----------

    private List<object> ApplyFilters(List<object> rows, IEnumerable<SavedViewFilter> filters, IReadOnlyDictionary<string, FieldRegistryEntry> registry)
    {
        // Composition rule (mirrors the facets the operators were derived from): rows sharing a
        // FieldKey with Eq/In OR together (membership); everything else ANDs.
        foreach (var group in filters.GroupBy(f => f.FieldKey))
        {
            var entry = registry[group.Key];
            var membership = group.Where(f => f.Operator is ViewOperator.Eq or ViewOperator.In).ToList();
            var comparisons = group.Where(f => f.Operator is not (ViewOperator.Eq or ViewOperator.In)).ToList();

            if (membership.Count > 0)
                rows = rows.Where(r => membership.Any(f => Matches(r, f, entry))).ToList();
            foreach (var f in comparisons)
                rows = rows.Where(r => Matches(r, f, entry)).ToList();
        }
        return rows;
    }

    private bool Matches(object row, SavedViewFilter f, FieldRegistryEntry entry)
    {
        var value = Prop(row, f.FieldKey);
        switch (f.Operator)
        {
            case ViewOperator.Eq:
            case ViewOperator.In:
                if (entry.DataType == FieldDataType.Tags && value is IEnumerable<string> tags)
                    return tags.Any(t => string.Equals(t, f.Value, StringComparison.OrdinalIgnoreCase));
                if (entry.DataType == FieldDataType.Bool)
                    return value is bool b && b == bool.Parse(f.Value);
                if (entry.DataType is FieldDataType.Money or FieldDataType.Number)
                    return value is not null && ToDecimal(value) == decimal.Parse(f.Value, System.Globalization.CultureInfo.InvariantCulture);
                return string.Equals(value?.ToString(), f.Value, StringComparison.OrdinalIgnoreCase);

            case ViewOperator.Contains:
                if (entry.DataType == FieldDataType.Tags && value is IEnumerable<string> list)
                    return list.Any(t => t.Contains(f.Value, StringComparison.OrdinalIgnoreCase));
                return value?.ToString()?.Contains(f.Value, StringComparison.OrdinalIgnoreCase) == true;

            case ViewOperator.Gte:
                return Compare(value, f.Value, entry) is { } c1 && c1 >= 0;
            case ViewOperator.Lte:
                return Compare(value, f.Value, entry) is { } c2 && c2 <= 0;
            case ViewOperator.Between:
                return Compare(value, f.Value, entry) is { } lo && lo >= 0 &&
                       Compare(value, f.Value2 ?? f.Value, entry) is { } hi && hi <= 0;
            default:
                throw new ViewValidationException($"Unsupported operator {f.Operator}.");
        }
    }

    /// <summary>row-value compared to the (token-resolved) filter value; null when the row value is null.</summary>
    private int? Compare(object? value, string raw, FieldRegistryEntry entry)
    {
        if (value is null) return null;
        switch (entry.DataType)
        {
            case FieldDataType.Date:
            {
                var bound = ResolveDateToken(raw, asEnd: false).Date;
                return ((DateOnly)value).CompareTo(DateOnly.FromDateTime(bound));
            }
            case FieldDataType.Instant:
            {
                var bound = ResolveDateToken(raw, asEnd: true);
                return ((DateTime)value).CompareTo(bound);
            }
            case FieldDataType.Money:
            case FieldDataType.Number:
                return ToDecimal(value).CompareTo(decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture));
            default:
                return string.Compare(value.ToString(), raw, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>The three ruled tokens (@today, @startOfMonth, @endOfMonth) — gate-driven set,
    /// resolved against IClock at run time so "closing this month" stays true next month.
    /// For instants, @endOfMonth/@today resolve to the END of that day when used as an upper
    /// bound (asEnd) so lte includes the whole day.</summary>
    private DateTime ResolveDateToken(string raw, bool asEnd)
    {
        var now = clock.UtcNow;
        DateTime? day = raw switch
        {
            "@today" => now.Date,
            "@startOfMonth" => new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            "@endOfMonth" => new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month), 0, 0, 0, DateTimeKind.Utc),
            _ => null,
        };
        if (day is { } d)
            return asEnd && raw != "@startOfMonth" ? d.AddDays(1).AddTicks(-1) : d;
        if (raw.StartsWith('@'))
            throw new ViewValidationException($"Unknown date token '{raw}' — supported: {string.Join(", ", ViewVocabulary.DateTokens)}.");
        return DateTime.Parse(raw, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
    }

    private static List<object> ApplySort(List<object> rows, IEnumerable<SavedViewColumn> columns, IReadOnlyDictionary<string, FieldRegistryEntry> registry)
    {
        var sorted = columns.OrderBy(c => c.Sort).FirstOrDefault(c => c.SortDirection is not null);
        if (sorted is null) return rows;
        var asc = sorted.SortDirection == ViewSortDirection.Asc;
        return asc
            ? rows.OrderBy(r => Prop(r, sorted.FieldKey)).ToList()
            : rows.OrderByDescending(r => Prop(r, sorted.FieldKey)).ToList();
    }

    // ---------- validation ----------

    private async Task ValidateDefinitionAsync(RecordType type, SaveViewRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new ViewValidationException("A view needs a name.");
        if (req.Columns.Count == 0)
            throw new ViewValidationException("A view needs at least one column.");

        var registry = await RegistryFor(type, ct);
        foreach (var key in req.Filters.Select(f => f.FieldKey).Concat(req.Columns.Select(c => c.FieldKey)))
            if (!registry.ContainsKey(key))
                throw new ViewValidationException($"Unknown field '{key}' for {type} — not in the field registry.");

        foreach (var f in req.Filters)
        {
            if (!Enum.TryParse<ViewOperator>(f.Operator, ignoreCase: true, out var op))
                throw new ViewValidationException($"Unknown operator '{f.Operator}'.");
            var dt = registry[f.FieldKey].DataType;
            var ok = op switch
            {
                ViewOperator.Eq or ViewOperator.In => true,
                ViewOperator.Contains => dt is FieldDataType.Code or FieldDataType.Text or FieldDataType.Enum or FieldDataType.Tags,
                ViewOperator.Between or ViewOperator.Gte or ViewOperator.Lte =>
                    dt is FieldDataType.Date or FieldDataType.Instant or FieldDataType.Money or FieldDataType.Number,
                _ => false,
            };
            if (!ok)
                throw new ViewValidationException($"Operator {op} is not valid for {dt} field '{f.FieldKey}'.");
            if (op == ViewOperator.Between && string.IsNullOrWhiteSpace(f.Value2))
                throw new ViewValidationException($"Between on '{f.FieldKey}' needs both bounds.");
            if (dt is FieldDataType.Date or FieldDataType.Instant)
            {
                ResolveDateToken(f.Value, asEnd: false);                       // throws on unknown token / unparsable date
                if (f.Value2 is not null) ResolveDateToken(f.Value2, asEnd: true);
            }
        }
    }

    private void RequireViewAction(RecordType type)
    {
        var action = ViewVocabulary.ViewActionFor[type];
        if (!ActionCatalog.RolesFor(action).Any(user.Roles.Contains))
            throw new ForbiddenException("Not permitted for your role.");
    }

    // ---------- helpers ----------

    private async Task<IReadOnlyDictionary<string, FieldRegistryEntry>> RegistryFor(RecordType type, CancellationToken ct) =>
        await db.FieldRegistry.AsNoTracking().Where(f => f.RecordType == type)
            .ToDictionaryAsync(f => f.FieldKey, ct);

    private static RecordType ParseRecordType(string raw) =>
        Enum.TryParse<RecordType>(raw, ignoreCase: true, out var t)
            ? t
            : throw new ViewValidationException($"Unknown record type '{raw}'.");

    private static void ApplyDefinition(SavedView view, SaveViewRequest req)
    {
        view.Filters.AddRange(req.Filters.Select((f, i) => new SavedViewFilter
        {
            SavedViewId = view.Id,
            FieldKey = f.FieldKey,
            Operator = Enum.Parse<ViewOperator>(f.Operator, ignoreCase: true),
            Value = f.Value,
            Value2 = f.Value2,
            Sort = i,
        }));
        view.Columns.AddRange(req.Columns.Select((c, i) => new SavedViewColumn
        {
            SavedViewId = view.Id,
            FieldKey = c.FieldKey,
            Label = c.Label,
            Sort = i,
            SortDirection = c.SortDirection is null ? null : Enum.Parse<ViewSortDirection>(c.SortDirection, ignoreCase: true),
        }));
    }

    private static SavedViewDto ToDto(SavedView v) => new(
        v.Id, v.Code, v.Name, v.RecordType.ToString(), v.OwnerUserId, v.IsShared, v.IsSystem,
        v.Filters.OrderBy(f => f.Sort).Select(f => new SavedViewFilterDto(f.FieldKey, f.Operator.ToString(), f.Value, f.Value2)).ToList(),
        v.Columns.OrderBy(c => c.Sort).Select(c => new SavedViewColumnDto(c.FieldKey, c.Label, c.SortDirection?.ToString())).ToList());

    private static readonly Dictionary<(Type, string), PropertyInfo?> PropCache = [];
    private static object? Prop(object row, string key)
    {
        var cacheKey = (row.GetType(), key);
        if (!PropCache.TryGetValue(cacheKey, out var pi))
            PropCache[cacheKey] = pi = row.GetType().GetProperty(key);
        return pi?.GetValue(row);
    }

    private static decimal ToDecimal(object v) => v switch
    {
        decimal d => d,
        int i => i,
        _ => Convert.ToDecimal(v, System.Globalization.CultureInfo.InvariantCulture),
    };
}

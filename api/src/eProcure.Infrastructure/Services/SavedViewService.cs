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

        // D5: deactivated custom defs hide from the builder palette (values persist; a view
        // still referencing one fails loudly at run). ListValue options come from the bound list.
        var defs = await db.CustomFieldDefs.AsNoTracking().Where(d => d.RecordType == type).ToListAsync(ct);
        var defByKey = defs.ToDictionary(d => d.Code, StringComparer.OrdinalIgnoreCase);
        var listIds = defs.Where(d => d.CustomListId is not null).Select(d => d.CustomListId!.Value).ToList();
        var listOptions = (await db.CustomListValues.AsNoTracking()
                .Where(v => listIds.Contains(v.CustomListId)).ToListAsync(ct))
            .GroupBy(v => v.CustomListId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.OrderBy(v => v.Sort).Select(v => v.Code).ToList());

        // D6: Segment-kind rows get options from their values (active), like ListValue fields.
        var segDefIds = rows.Where(r => r.Kind == FieldKind.Segment && r.SegmentDefId is not null)
            .Select(r => r.SegmentDefId!.Value).ToList();
        var segOptions = (await db.SegmentValues.AsNoTracking()
                .Where(v => segDefIds.Contains(v.SegmentDefId) && v.Active).ToListAsync(ct))
            .GroupBy(v => v.SegmentDefId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.OrderBy(v => v.Sort).Select(v => v.Code).ToList());

        return rows
            .Where(f => f.Kind != FieldKind.Custom || (defByKey.TryGetValue(f.FieldKey, out var d) && d.Active))
            .Select(f => new ViewFieldDto(
                f.FieldKey, f.Label, f.DataType.ToString(), f.Kind.ToString(),
                f.Kind == FieldKind.Custom
                    ? (defByKey.TryGetValue(f.FieldKey, out var d) && d.CustomListId is { } lid ? listOptions.GetValueOrDefault(lid) : null)
                    : f.Kind == FieldKind.Segment
                        ? (f.SegmentDefId is { } sdid ? segOptions.GetValueOrDefault(sdid) : null)
                        : (ViewVocabulary.EnumOptions.TryGetValue((type, f.FieldKey), out var opts) ? opts : null)))
            .ToList();
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

        await LoadCustomStateAsync(view.RecordType, ct);
        foreach (var key in view.Filters.Select(f => f.FieldKey).Concat(view.Columns.Select(c => c.FieldKey)))
            if (_inactiveCustomKeys.Contains(key))
                throw new ViewValidationException($"Custom field '{key}' is deactivated — fix or remove it from the view (D5 lifecycle rule).");

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
            foreach (var c in columns) d[c.FieldKey] = Val(row, c.FieldKey);
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

    public async Task<Application.Dashboards.ViewAggregateResult> AggregateAsync(Guid id, string fn, string? fieldKey, string? groupBy = null, CancellationToken ct = default)
    {
        var (view, registry, rows) = await PrepareAsync(id, ct);
        fn = fn.ToLowerInvariant();

        // D6: group-by-segment — one slice per value present + the NAMED Unassigned group
        // (honest-null applied to dimensions; ruled its own test).
        if (groupBy is not null)
        {
            if (!_segmentValues.TryGetValue(groupBy, out var segMap))
                throw new ViewValidationException($"'{groupBy}' is not a segment applied to {view.RecordType}.");
            var labels = _segmentLabels[groupBy];
            var groups = rows
                .GroupBy(r => segMap.GetValueOrDefault((Guid)Prop(r, "Id")!) as string)
                .Select(g => new Application.Dashboards.ViewAggregateGroup(
                    g.Key ?? "__unassigned", g.Key is null ? "Unassigned" : labels.GetValueOrDefault(g.Key, g.Key),
                    FoldGroup(fn, fieldKey, registry, g.ToList())))
                .OrderByDescending(g => g.Value ?? 0).ToList();
            var total = fn == "count" ? rows.Count : (decimal?)groups.Sum(g => g.Value ?? 0);
            return new(view.Id, fn, fn == "count" ? null : fieldKey, total,
                rows.Count == 0 ? 0 : rows.Count(r => Val(r, fieldKey ?? "Id") is null && fn != "count"),
                groupBy, groups);
        }

        if (fn == "count")
            return new(view.Id, fn, null, rows.Count, 0);

        if (fieldKey is not null && _inactiveCustomKeys.Contains(fieldKey))
            throw new ViewValidationException($"Custom field '{fieldKey}' is deactivated.");
        var entry = RequireNumericField(fn, fieldKey, registry);
        var values = rows.Select(r => Val(r, entry.FieldKey)).ToList();
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

    public async Task<Application.Dashboards.ViewSeriesResult> SeriesAsync(Guid id, string fn, string? fieldKey, string bucketField, int months, string? groupBy = null, CancellationToken ct = default)
    {
        var (view, registry, rows) = await PrepareAsync(id, ct);
        fn = fn.ToLowerInvariant();
        if (_inactiveCustomKeys.Contains(bucketField) || (fieldKey is not null && _inactiveCustomKeys.Contains(fieldKey)))
            throw new ViewValidationException("A deactivated custom field cannot drive a series.");
        if (!registry.TryGetValue(bucketField, out var bucket) ||
            bucket.DataType is not (FieldDataType.Date or FieldDataType.Instant))
            throw new ViewValidationException($"Series bucket field must be a Date/Instant registry key; '{bucketField}' is not.");
        FieldRegistryEntry? entry = fn == "count" ? null : RequireNumericField(fn, fieldKey, registry);

        months = Math.Clamp(months, 3, 36);
        var now = clock.UtcNow;
        var first = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(months - 1));
        var keys = Enumerable.Range(0, months).Select(i => first.AddMonths(i).ToString("yyyy-MM")).ToList();

        string? BucketOf(object row) => Val(row, bucketField) switch
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
            var nums = bucketRows.Select(r => Val(r, entry!.FieldKey)).Where(v => v is not null).Select(v => ToDecimal(v!)).ToList();
            return nums.Count == 0 ? 0m : fn == "sum" ? nums.Sum() : Math.Round(nums.Average(), 2);
        }

        var buckets = keys.Select(k => new Application.Dashboards.SeriesBucketDto(k, ValueOf(groups.GetValueOrDefault(k)))).ToList();

        // D6: grouped series — one series per segment value present + the Unassigned series.
        List<Application.Dashboards.ViewSeriesGroup>? grouped = null;
        if (groupBy is not null)
        {
            if (!_segmentValues.TryGetValue(groupBy, out var segMap))
                throw new ViewValidationException($"'{groupBy}' is not a segment applied to {view.RecordType}.");
            var labels = _segmentLabels[groupBy];
            grouped = rows
                .GroupBy(r => segMap.GetValueOrDefault((Guid)Prop(r, "Id")!) as string)
                .Select(g =>
                {
                    var byMonth = g.Select(r => (Key: BucketOf(r), Row: r)).Where(x => x.Key is not null)
                        .GroupBy(x => x.Key!).ToDictionary(x => x.Key, x => x.Select(y => y.Row).ToList());
                    return new Application.Dashboards.ViewSeriesGroup(
                        g.Key ?? "__unassigned", g.Key is null ? "Unassigned" : labels.GetValueOrDefault(g.Key, g.Key),
                        keys.Select(k => new Application.Dashboards.SeriesBucketDto(k, ValueOf(byMonth.GetValueOrDefault(k)))).ToList());
                }).ToList();
        }
        return new(view.Id, fn, entry?.FieldKey, bucketField, buckets, unbucketed, groupBy, grouped);
    }

    /// <summary>Fold one group's rows: count, or sum/avg over the field with honest nulls.</summary>
    private decimal? FoldGroup(string fn, string? fieldKey, IReadOnlyDictionary<string, FieldRegistryEntry> registry, List<object> rows)
    {
        if (fn == "count") return rows.Count;
        var entry = RequireNumericField(fn, fieldKey, registry);
        var nums = rows.Select(r => Val(r, entry.FieldKey)).Where(v => v is not null).Select(v => ToDecimal(v!)).ToList();
        return fn switch
        {
            "sum" => rows.Count > 0 && nums.Count == 0 ? null : nums.Sum(),
            "avg" => nums.Count == 0 ? null : Math.Round(nums.Average(), 2),
            _ => throw new ViewValidationException($"Unknown aggregate fn '{fn}' — count|sum|avg."),
        };
    }

    private static FieldRegistryEntry RequireNumericField(string fn, string? fieldKey, IReadOnlyDictionary<string, FieldRegistryEntry> registry)
    {
        if (fieldKey is null || !registry.TryGetValue(fieldKey, out var entry))
            throw new ViewValidationException($"fn={fn} needs a registry fieldKey.");
        if (entry.DataType is not (FieldDataType.Money or FieldDataType.Number))
            throw new ViewValidationException($"fn={fn} needs a Money/Number field; '{fieldKey}' is {entry.DataType}.");
        return entry;
    }

    /// <summary>D5: load the record type's custom defs + values once per run — typed objects
    /// so every operator/comparison path works unchanged on custom keys.</summary>
    private async Task LoadCustomStateAsync(RecordType type, CancellationToken ct)
    {
        await LoadSegmentStateAsync(type, ct);
        var defs = await db.CustomFieldDefs.AsNoTracking().Where(d => d.RecordType == type).ToListAsync(ct);
        _inactiveCustomKeys = defs.Where(d => !d.Active).Select(d => d.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (defs.Count == 0) { _customValues = []; return; }

        var byId = defs.ToDictionary(d => d.Id, d => d.Code);
        var values = await db.CustomFieldValues.AsNoTracking().Where(v => v.RecordType == type).ToListAsync(ct);
        _customValues = defs.ToDictionary(d => d.Code, _ => new Dictionary<Guid, object?>(), StringComparer.OrdinalIgnoreCase);
        foreach (var v in values)
        {
            if (!byId.TryGetValue(v.FieldDefId, out var code)) continue;
            object? typed = v switch
            {
                { ValueText: { } s } => s,
                { ValueNumber: { } n } => n,
                { ValueMoney: { } m } => m,
                { ValueDate: { } dt } => dt,
                { ValueBool: { } bl } => bl,
                { ValueListCode: { } c } => c,
                _ => null,
            };
            _customValues[code][v.RecordId] = typed;
        }
    }

    /// <summary>D6: header-level segment assignments per FieldKey (seg_*) — value CODES, so
    /// Eq/In/Contains filter semantics match ListValue custom fields. Absent assignment =
    /// null = the honest Unassigned. Also feeds the group-by capability by def code.</summary>
    private Dictionary<string, Dictionary<Guid, object?>> _segmentValues = [];
    private Dictionary<string, Dictionary<string, string>> _segmentLabels = [];   // defCode -> valueCode -> label

    private async Task LoadSegmentStateAsync(RecordType type, CancellationToken ct)
    {
        _segmentValues = []; _segmentLabels = [];
        var apps = await db.SegmentApplications.AsNoTracking().Where(a => a.RecordType == type).ToListAsync(ct);
        if (apps.Count == 0) return;
        var defIds = apps.Select(a => a.SegmentDefId).ToList();
        var defs = await db.SegmentDefs.AsNoTracking().Where(d => defIds.Contains(d.Id)).ToListAsync(ct);
        var values = await db.SegmentValues.AsNoTracking().Where(v => defIds.Contains(v.SegmentDefId)).ToListAsync(ct);
        var valueById = values.ToDictionary(v => v.Id);
        var assignments = await db.SegmentAssignments.AsNoTracking()
            .Where(a => defIds.Contains(a.SegmentDefId) && a.RecordType == type && a.LineId == null).ToListAsync(ct);
        foreach (var d in defs)
        {
            var map = new Dictionary<Guid, object?>();
            foreach (var a in assignments.Where(a => a.SegmentDefId == d.Id))
                map[a.RecordId] = valueById.GetValueOrDefault(a.SegmentValueId)?.Code;
            _segmentValues[d.Code] = map;
            _segmentLabels[d.Code] = values.Where(v => v.SegmentDefId == d.Id).ToDictionary(v => v.Code, v => v.Label);
        }
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
        var value = Val(row, f.FieldKey);
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
        // D5 (ruled): the @today±Nd token FORM — a parser extension, no new token names.
        if (day is null && System.Text.RegularExpressions.Regex.Match(raw, @"^@today([+-]\d{1,4})d$") is { Success: true } offset)
            day = now.Date.AddDays(int.Parse(offset.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
        if (day is { } d)
            return asEnd && raw != "@startOfMonth" ? d.AddDays(1).AddTicks(-1) : d;
        if (raw.StartsWith('@'))
            throw new ViewValidationException($"Unknown date token '{raw}' — supported: {string.Join(", ", ViewVocabulary.DateTokens)} and the @today±Nd form.");
        return DateTime.Parse(raw, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
    }

    private List<object> ApplySort(List<object> rows, IEnumerable<SavedViewColumn> columns, IReadOnlyDictionary<string, FieldRegistryEntry> registry)
    {
        var sorted = columns.OrderBy(c => c.Sort).FirstOrDefault(c => c.SortDirection is not null);
        if (sorted is null) return rows;
        var asc = sorted.SortDirection == ViewSortDirection.Asc;
        return asc
            ? rows.OrderBy(r => Val(r, sorted.FieldKey)).ToList()
            : rows.OrderByDescending(r => Val(r, sorted.FieldKey)).ToList();
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

    // ---- D5: custom-field resolution ----
    // Per-run map: custom FieldKey -> (RecordId -> typed value). Populated in PrepareAsync;
    // scoped service = one run per instance. Native keys fall through to reflection.
    private Dictionary<string, Dictionary<Guid, object?>> _customValues = [];
    private HashSet<string> _inactiveCustomKeys = [];

    /// <summary>Field resolution: Custom-kind keys read the value map (absent row = null —
    /// the honest null the aggregate/series null semantics count); native keys read the DTO.</summary>
    private object? Val(object row, string key)
    {
        if (_customValues.TryGetValue(key, out var map))
            return Prop(row, "Id") is Guid id ? map.GetValueOrDefault(id) : null;
        if (_segmentValues.TryGetValue(key, out var seg))
            return Prop(row, "Id") is Guid sid ? seg.GetValueOrDefault(sid) : null;
        return Prop(row, key);
    }

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

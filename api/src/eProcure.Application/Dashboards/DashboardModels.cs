namespace eProcure.Application.Dashboards;

// ---- Metric layer (D4 Step 0(b)) ----

/// <summary>A system metric's identity card. RequiredAction is the AUTHORIZATION-MATRIX
/// action a caller must hold to read it (checked dynamically on value/series — the same
/// machinery as the view run's record-type check).</summary>
public sealed record MetricDescriptor(string Id, string Label, string Unit, string RequiredAction, bool IsSeries);

/// <summary>Honest-null: Value null + NotYetAvailable=true renders "not yet available" —
/// never zero (Slice H posture). ExcludedNullCount surfaces rows the compute had to skip
/// (e.g. invoices without a Date).</summary>
public sealed record MetricValueDto(string Id, string Label, string Unit, decimal? Value, bool NotYetAvailable, int ExcludedNullCount);

public sealed record SeriesBucketDto(string Bucket, decimal Value);

/// <summary>UnbucketedCount = rows whose bucket field is null — excluded, surfaced, never
/// silently thinning the chart.</summary>
public sealed record MetricSeriesDto(string Id, string Label, string Unit, IReadOnlyList<SeriesBucketDto> Buckets, int UnbucketedCount);

public interface ISystemMetricService
{
    IReadOnlyList<MetricDescriptor> Catalog { get; }
    Task<MetricValueDto> ValueAsync(string id, CancellationToken ct = default);
    Task<MetricSeriesDto> SeriesAsync(string id, int months, CancellationToken ct = default);
}

// ---- View aggregation (D4 Step 0(c) — rides the D3 machinery) ----

/// <summary>D6 group-by: one slice per segment value present, PLUS the named Unassigned
/// group — honest-null applied to dimensions, never a silently dropped record.</summary>
public sealed record ViewAggregateGroup(string Key, string Label, decimal? Value);

public sealed record ViewAggregateResult(
    Guid ViewId, string Fn, string? FieldKey, decimal? Value, int ExcludedNullCount,
    string? GroupedBy = null, IReadOnlyList<ViewAggregateGroup>? Groups = null);

public sealed record ViewSeriesGroup(string Key, string Label, IReadOnlyList<SeriesBucketDto> Buckets);

public sealed record ViewSeriesResult(
    Guid ViewId, string Fn, string? FieldKey, string BucketField, IReadOnlyList<SeriesBucketDto> Buckets, int UnbucketedCount,
    string? GroupedBy = null, IReadOnlyList<ViewSeriesGroup>? Series = null);

// ---- Dashboards API ----

public sealed record PortletDto(Guid Id, string PortletType, string Title, short Col, short Row, short Width, Guid? SavedViewId, string ConfigJson);

public sealed record UserDashboardDto(Guid Id, string Name, bool IsPersonalized, IReadOnlyList<PortletDto> Portlets);

public sealed record PortletUpsert(Guid? Id, string PortletType, string Title, short Col, short Row, short Width, Guid? SavedViewId, string ConfigJson);

public sealed record UpdateDashboardRequest(string? Name, List<PortletUpsert> Portlets);

public interface IDashboardStore
{
    /// <summary>The caller's dashboard: their personalized copy if one exists, else the
    /// deduplicated UNION of their roles' defaults (reproduces the legacy server merge).</summary>
    Task<UserDashboardDto> MineAsync(CancellationToken ct = default);
    Task<UserDashboardDto> PersonalizeAsync(CancellationToken ct = default);
    Task<UserDashboardDto> UpdateMineAsync(UpdateDashboardRequest req, CancellationToken ct = default);
    Task ResetAsync(CancellationToken ct = default);
    Task<UserDashboardDto> UpdateRoleDefaultAsync(string role, UpdateDashboardRequest req, CancellationToken ct = default);
}

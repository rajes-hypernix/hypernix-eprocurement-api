using System.Text.Json;
using eProcure.Domain.Dashboards;

namespace eProcure.Application.Dashboards;

/// <summary>An invalid portlet configuration → HTTP 400, same loud-validation posture as views.</summary>
public sealed class DashboardValidationException(string message) : Exception(message);

/// <summary>
/// THE schema of PortletInstance.ConfigJson per portlet type — the framework's ONE
/// sanctioned JSON, typed here and validated on every save (and by the seed drift-test).
/// A KpiMeter is backed EITHER by a system metric (MetricId) OR by a saved view
/// (the instance's SavedViewId + Fn [+ FieldKey for sum/avg]).
/// </summary>
public static class PortletConfigs
{
    // GroupBy (D6): a Segment fieldKey — the KPI renders one slice per value + Unassigned.
    public sealed record KpiMeterConfig(string? MetricId, string? Fn, string? FieldKey, decimal? Target, string? Link, string? GroupBy = null);
    public sealed record ScorecardItem(string MetricId, string? Link);
    public sealed record KpiScorecardConfig(List<ScorecardItem> Items);
    public sealed record ReminderItem(Guid SavedViewId, string Label, string Route);
    public sealed record RemindersConfig(List<ReminderItem> Items);
    public sealed record SavedViewListConfig(int TopN, string? Route);
    public sealed record ShortcutItem(string Label, string Route, string? Action, string? Color = null);   // CF3-T10: tile colour (hex), optional
    public sealed record ShortcutsConfig(List<ShortcutItem> Items);
    public sealed record ChartConfig(List<string> SeriesIds, int Months, int MinMonths);
    // RecentRecords and MyInvitations carry no config ({}).

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Parse-validate a portlet's config against its type's schema. Loud, never silent.</summary>
    public static void Validate(PortletType type, string configJson, Guid? savedViewId)
    {
        try
        {
            switch (type)
            {
                case PortletType.KpiMeter:
                {
                    var c = JsonSerializer.Deserialize<KpiMeterConfig>(configJson, Json)
                        ?? throw new DashboardValidationException("KpiMeter needs a config.");
                    var viewBacked = savedViewId is not null;
                    if (viewBacked == (c.MetricId is not null))
                        throw new DashboardValidationException("KpiMeter is backed by EITHER a system metric OR a saved view — exactly one.");
                    if (viewBacked && c.Fn is null)
                        throw new DashboardValidationException("A view-backed KpiMeter needs fn (count|sum|avg).");
                    if (c.Fn is "sum" or "avg" && c.FieldKey is null)
                        throw new DashboardValidationException($"fn={c.Fn} needs a Money/Number fieldKey.");
                    if (c.GroupBy is not null && !viewBacked)
                        throw new DashboardValidationException("groupBy slices a view-backed KPI — metric-backed KPIs have no dimension.");
                    break;
                }
                case PortletType.KpiScorecard:
                {
                    var c = JsonSerializer.Deserialize<KpiScorecardConfig>(configJson, Json);
                    if (c is null || c.Items.Count == 0)
                        throw new DashboardValidationException("KpiScorecard needs at least one item.");
                    break;
                }
                case PortletType.Reminders:
                {
                    var c = JsonSerializer.Deserialize<RemindersConfig>(configJson, Json);
                    if (c is null || c.Items.Count == 0)
                        throw new DashboardValidationException("Reminders needs at least one item.");
                    break;
                }
                case PortletType.SavedViewList:
                {
                    var c = JsonSerializer.Deserialize<SavedViewListConfig>(configJson, Json)
                        ?? throw new DashboardValidationException("SavedViewList needs a config.");
                    if (savedViewId is null) throw new DashboardValidationException("SavedViewList needs a SavedViewId.");
                    if (c.TopN is < 1 or > 50) throw new DashboardValidationException("SavedViewList TopN must be 1–50.");
                    break;
                }
                case PortletType.Shortcuts:
                {
                    var c = JsonSerializer.Deserialize<ShortcutsConfig>(configJson, Json);
                    if (c is null || c.Items.Count == 0)
                        throw new DashboardValidationException("Shortcuts needs at least one item.");
                    break;
                }
                case PortletType.Chart:
                {
                    var c = JsonSerializer.Deserialize<ChartConfig>(configJson, Json)
                        ?? throw new DashboardValidationException("Chart needs a config.");
                    if (c.SeriesIds.Count == 0) throw new DashboardValidationException("Chart needs at least one series.");
                    if (c.Months is < 3 or > 36) throw new DashboardValidationException("Chart months must be 3–36.");
                    break;
                }
                case PortletType.RecentRecords:
                case PortletType.MyInvitations:
                    _ = JsonSerializer.Deserialize<JsonElement>(configJson, Json);   // must be valid JSON; content ignored
                    break;
                default:
                    throw new DashboardValidationException($"Unknown portlet type {type}.");
            }
        }
        catch (JsonException e)
        {
            throw new DashboardValidationException($"Portlet config is not valid JSON for {type}: {e.Message}");
        }
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using eProcure.Domain.Dashboards;
using eProcure.Domain.Identity;

namespace eProcure.Application.Dashboards;

/// <summary>
/// The six role-default dashboards (D4 Step 0(a)/(d), as ruled — CommEvaluator included),
/// reproducing the census's COMPUTED content per role. THE single source: the Dashboards
/// migration inserts these rows, tests seed from them, and the config drift-test validates
/// every seeded portlet against its type schema. Deterministic ids per (role, index).
/// </summary>
public static class DashboardSeed
{
    public sealed record PortletSeed(PortletType Type, string Title, short Col, short Row, short Width, Guid? SavedViewId, string ConfigJson);
    public sealed record DashboardSeedRow(string Code, string Name, string OwnerRole, IReadOnlyList<PortletSeed> Portlets);

    /// <summary>The shared "Recent purchase orders" system view the SavedViewList portlet rides.</summary>
    public static readonly Guid RecentPosViewId = new("d4000000-0000-0000-0000-00000000b001");

    /// <summary>D3's "All RFQs" system view — the Reminders portlet's click-through target.</summary>
    public static readonly Guid AllRfqsViewId = new("d3000000-0000-0000-0000-00000000a11f");

    public static Guid DashboardId(string role) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"dashboard:role:{role}")));
    public static Guid PortletId(string role, int index) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"portlet:{role}:{index}")));

    private static string J<T>(T config) => JsonSerializer.Serialize(config, PortletConfigs.Json);

    private static PortletSeed Scorecard(string title, short row, params PortletConfigs.ScorecardItem[] items) =>
        new(PortletType.KpiScorecard, title, 0, row, 2, null, J(new PortletConfigs.KpiScorecardConfig([.. items])));

    private static PortletSeed Meter(string title, short col, short row, string metricId, string? link, short width = 1) =>
        new(PortletType.KpiMeter, title, col, row, width, null, J(new PortletConfigs.KpiMeterConfig(metricId, null, null, null, link)));

    private static readonly PortletSeed SpendChart = new(
        PortletType.Chart, "Committed spend (RM/month, actuals)", 0, 1, 1, null,
        J(new PortletConfigs.ChartConfig([MetricIds.SpendByMonth], 12, 3)));

    private static readonly PortletSeed OnboardedChart = new(
        PortletType.Chart, "Vendors onboarded / month", 1, 1, 1, null,
        J(new PortletConfigs.ChartConfig([MetricIds.VendorsOnboardedSwecByMonth, MetricIds.VendorsOnboardedNonSwecByMonth], 12, 1)));

    private static readonly PortletSeed RecentPos = new(
        PortletType.SavedViewList, "Recent purchase orders", 0, 3, 2, RecentPosViewId,
        J(new PortletConfigs.SavedViewListConfig(5, "pos")));

    public static readonly IReadOnlyList<DashboardSeedRow> Rows =
    [
        new("DASH-SYS-BUYER", "Sourcing Dashboard", Roles.Buyer,
        [
            Scorecard("Sourcing pipeline", 0,
                new(MetricIds.OpenRequisitions, "reqs"), new(MetricIds.RfqsAwaitingBids, "rfqs"),
                new(MetricIds.RfqsReadyToOpen, "rfqs"), new(MetricIds.RfqsUnderEvaluation, "rfqs")),
            SpendChart,
            OnboardedChart,
            Meter("Committed spend MTD", 0, 2, MetricIds.CommittedSpendMtd, "invoices"),
            Meter("vs same month last year", 1, 2, MetricIds.SpendVsSameMonthLy, null),
            RecentPos,
            // Appended by the DashboardSeedRefresh migration (indexes 6–8 — earlier ids stay stable):
            // the remaining portlet types live on the buyer default so every type ships REAL.
            new(PortletType.Reminders, "Reminders", 0, 4, 1, null,
                J(new PortletConfigs.RemindersConfig([new(AllRfqsViewId, "All RFQs", "rfqs")]))),
            new(PortletType.Shortcuts, "Shortcuts", 1, 4, 1, null,
                J(new PortletConfigs.ShortcutsConfig([
                    new("New requisition", "reqs", "ManageRequisitions"),
                    new("Consolidate to RFQ", "consolidate", "ManageRfqDraft"),
                    new("Vendor master", "vendors", null),
                    new("Invite a vendor", "onboarding/invite", "InviteOnboarding"),
                ]))),
            new(PortletType.RecentRecords, "Recent records", 0, 5, 2, null, "{}"),
        ]),

        new("DASH-SYS-VENDOR", "Vendor Dashboard", Roles.Vendor,
        [
            Scorecard("Your work queue", 0,
                new(MetricIds.VendorRfqsToBid, "dashboard"), new(MetricIds.VendorBidsSubmitted, "bids"),
                new(MetricIds.VendorPosToAcknowledge, "pos"), new(MetricIds.VendorOpenPos, "pos")),
            new(PortletType.MyInvitations, "RFQ invitations", 0, 1, 2, null, "{}"),   // OD-D4-2: the eighth type
        ]),

        new("DASH-SYS-APPROVER", "Approvals", Roles.Approver,
        [
            Meter("Awards to approve", 0, 0, MetricIds.AwardsToApprove, "awards", width: 2),
        ]),

        new("DASH-SYS-TECHEVAL", "Evaluation", Roles.TechEvaluator,
        [
            Meter("Technical scoring pending", 0, 0, MetricIds.TechScoringPending, "openings", width: 2),
        ]),

        new("DASH-SYS-COMMEVAL", "Evaluation", Roles.CommEvaluator,
        [
            Meter("Commercial envelopes to open", 0, 0, MetricIds.CommOpeningsPending, "openings", width: 2),
        ]),

        new("DASH-SYS-ADMIN", "Sourcing Dashboard", Roles.Admin,
        [
            Scorecard("Platform", 0,
                new(MetricIds.OpenRequisitions, "reqs"), new(MetricIds.RfqsAwaitingBids, "rfqs"),
                new(MetricIds.RfqsReadyToOpen, "rfqs"), new(MetricIds.RfqsUnderEvaluation, "rfqs"),
                new(MetricIds.VendorCount, "vendors"), new(MetricIds.UserCount, "admin")),
            SpendChart,
            OnboardedChart,
            Meter("Committed spend MTD", 0, 2, MetricIds.CommittedSpendMtd, "invoices"),
            Meter("vs same month last year", 1, 2, MetricIds.SpendVsSameMonthLy, null),
            RecentPos,
        ]),
    ];
}

namespace eProcure.Application.Dashboards;

/// <summary>
/// System metric ids (D4 Step 0(b), as ruled) — the wire values portlet configs reference.
/// The catalog itself (label, unit, required action, IClock compute, null behaviour) lives in
/// Infrastructure's SystemMetricService; these constants keep seeds and configs typo-proof.
/// </summary>
public static class MetricIds
{
    // The four buyer stat cards — migrated byte-identical (OD-D4-1: openRequisitions keeps
    // the count-ALL query; the label-vs-query correction is a post-D4 BACKLOG row).
    public const string OpenRequisitions = "openRequisitions";
    public const string RfqsAwaitingBids = "rfqsAwaitingBids";
    public const string RfqsReadyToOpen = "rfqsReadyToOpen";
    public const string RfqsUnderEvaluation = "rfqsUnderEvaluation";

    // Admin
    public const string VendorCount = "vendorCount";
    public const string UserCount = "userCount";

    // Per-principal (SoD/scoping semantics migrate WITH these — test-pinned before the
    // legacy DashboardService dies, per the A1-retirement condition).
    public const string TechScoringPending = "techScoringPending";
    public const string CommOpeningsPending = "commOpeningsPending";
    public const string AwardsToApprove = "awardsToApprove";
    public const string VendorRfqsToBid = "vendorRfqsToBid";
    public const string VendorBidsSubmitted = "vendorBidsSubmitted";
    public const string VendorPosToAcknowledge = "vendorPosToAcknowledge";
    public const string VendorOpenPos = "vendorOpenPos";

    // Money/time metrics (honest-null per Slice H posture)
    public const string CommittedSpendMtd = "committedSpendMtd";
    public const string SpendVsSameMonthLy = "spendVsSameMonthLy";
    public const string PrToPoCycleDays = "prToPoCycleDays";

    // Series (month-bucketed, for Chart portlets)
    public const string SpendByMonth = "spendByMonth";
    public const string VendorsOnboardedSwecByMonth = "vendorsOnboardedSwecByMonth";
    public const string VendorsOnboardedNonSwecByMonth = "vendorsOnboardedNonSwecByMonth";
}

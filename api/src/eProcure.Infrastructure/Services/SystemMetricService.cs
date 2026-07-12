using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Authorization;
using eProcure.Application.Dashboards;
using eProcure.Domain.Procurement;
using eProcure.Domain.Sourcing;
using eProcure.Domain.Suppliers;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

/// <summary>
/// The system metric catalog (D4 Step 0(b), as ruled). Every compute is IClock-driven —
/// month windows come from the injected clock, never DB CURRENT_DATE — and every metric
/// carries its RequiredAction (checked here, deny-by-default) and its null behaviour:
/// backfill-null inputs render "not yet available", never zero (Slice H posture).
/// The per-principal computes replicate the retiring DashboardService BYTE-IDENTICALLY —
/// the Approver's creator≠me SoD and the evaluators' assigned-to-me scoping migrate here
/// WITH their semantics test-pinned (the A1-retirement condition).
/// </summary>
public sealed class SystemMetricService(AppDbContext db, IClock clock, ICurrentUser user) : ISystemMetricService
{
    private const string Count = "count";
    private const string Rm = "RM";
    private const string Days = "days";

    /// <summary>The catalog actions consumed DYNAMICALLY (per-metric RequiredAction, enforced
    /// in Require). The no-orphan sweep derives its dynamic-carrier set from this, so an
    /// action used only by a metric (A72 ViewSpendAnalytics) is not an orphan — and becomes
    /// one again the day its last metric retires.</summary>
    public static IReadOnlyCollection<string> CatalogRequiredActions =>
        Rows.Select(m => m.RequiredAction).Distinct().ToArray();

    public IReadOnlyList<MetricDescriptor> Catalog { get; } = Rows;

    private static readonly MetricDescriptor[] Rows =
    [
        // OD-D4-1 (ruled option a): the query stays byte-identical to DashboardService.cs:51
        // (counts ALL PRs); the label→query correction is a post-D4 BACKLOG row.
        new(MetricIds.OpenRequisitions, "Open requisitions", Count, ApiActions.ViewRequisitions, false),
        new(MetricIds.RfqsAwaitingBids, "RFQs awaiting bids", Count, ApiActions.ViewRfqs, false),
        new(MetricIds.RfqsReadyToOpen, "Ready to open", Count, ApiActions.ViewRfqs, false),
        new(MetricIds.RfqsUnderEvaluation, "Under evaluation", Count, ApiActions.ViewRfqs, false),
        new(MetricIds.VendorCount, "Vendors", Count, ApiActions.ViewUsers, false),                // A2F-T1: a management ROSTER COUNT, not the masked vendor list — rides the internal-only user-directory action (ruled: reuse, not mint)
        new(MetricIds.UserCount, "Users", Count, ApiActions.ViewUsers, false),
        new(MetricIds.TechScoringPending, "Technical scoring pending", Count, ApiActions.ViewBidOpenings, false),
        new(MetricIds.CommOpeningsPending, "Commercial envelopes to open", Count, ApiActions.ViewBidOpenings, false),
        new(MetricIds.AwardsToApprove, "Awards to approve", Count, ApiActions.ViewAwards, false),
        new(MetricIds.VendorRfqsToBid, "RFQs to bid", Count, ApiActions.ViewMyInvitations, false),
        new(MetricIds.VendorBidsSubmitted, "Bids submitted", Count, ApiActions.ViewMyInvitations, false),
        new(MetricIds.VendorPosToAcknowledge, "POs to acknowledge", Count, ApiActions.ViewMyInvitations, false),
        new(MetricIds.VendorOpenPos, "Open purchase orders", Count, ApiActions.ViewMyInvitations, false),
        new(MetricIds.CommittedSpendMtd, "Committed spend MTD", Rm, ApiActions.ViewSpendAnalytics, false),    // A2F-T1 (AUTHZ-1): org-wide spend — ViewInvoices let vendors (own-record readers) pull org totals
        new(MetricIds.SpendVsSameMonthLy, "vs same month last year", "%", ApiActions.ViewSpendAnalytics, false),  // A2F-T1 (AUTHZ-1)
        new(MetricIds.PrToPoCycleDays, "PR → PO cycle", Days, ApiActions.ViewPos, false),
        new(MetricIds.SpendByMonth, "Committed spend by month", Rm, ApiActions.ViewSpendAnalytics, true),     // A2F-T1 (AUTHZ-1)
        new(MetricIds.VendorsOnboardedSwecByMonth, "SWEC vendors onboarded", Count, ApiActions.ViewUsers, true),      // A2F.1 (T1 completion): org-wide ROSTER series — rides the same internal-only action as vendorCount
        new(MetricIds.VendorsOnboardedNonSwecByMonth, "Non-SWEC vendors onboarded", Count, ApiActions.ViewUsers, true),  // A2F.1 (T1 completion)
    ];

    public async Task<MetricValueDto> ValueAsync(string id, CancellationToken ct = default)
    {
        var d = Require(id, series: false);
        var (value, notYet, excluded) = id switch
        {
            MetricIds.OpenRequisitions => (await db.PurchaseRequisitions.CountAsync(ct), false, 0),
            MetricIds.RfqsAwaitingBids => (await db.Rfqs.CountAsync(r => r.Status == RfqStatus.Open, ct), false, 0),
            MetricIds.RfqsReadyToOpen => (await db.Rfqs.CountAsync(r => r.Status == RfqStatus.Closed, ct), false, 0),
            MetricIds.RfqsUnderEvaluation => (await db.Rfqs.CountAsync(r => r.Status == RfqStatus.Evaluation, ct), false, 0),
            MetricIds.VendorCount => (await db.Vendors.CountAsync(ct), false, 0),
            MetricIds.UserCount => (await db.Users.CountAsync(ct), false, 0),
            MetricIds.TechScoringPending => (await TechScoringPending(ct), false, 0),
            MetricIds.CommOpeningsPending => (await CommOpeningsPending(ct), false, 0),
            MetricIds.AwardsToApprove => (await AwardsToApprove(ct), false, 0),
            MetricIds.VendorRfqsToBid => (await VendorRfqsToBid(ct), false, 0),
            MetricIds.VendorBidsSubmitted => (await db.Bids.CountAsync(b => b.VendorId == RequireVendor() && b.Submitted, ct), false, 0),
            MetricIds.VendorPosToAcknowledge => (await db.PurchaseOrders.CountAsync(p => p.VendorId == RequireVendor() && p.Status == PoStatus.Issued && !p.Acknowledged, ct), false, 0),
            MetricIds.VendorOpenPos => (await VendorOpenPos(ct), false, 0),
            MetricIds.CommittedSpendMtd => await CommittedSpendMtd(ct),
            MetricIds.SpendVsSameMonthLy => await SpendVsSameMonthLy(ct),
            MetricIds.PrToPoCycleDays => await PrToPoCycleDays(ct),
            _ => throw new NotFoundException($"Metric {id} not found."),
        };
        return new MetricValueDto(d.Id, d.Label, d.Unit, notYet ? null : value, notYet, excluded);
    }

    public async Task<MetricSeriesDto> SeriesAsync(string id, int months, CancellationToken ct = default)
    {
        var d = Require(id, series: true);
        months = Math.Clamp(months, 3, 36);
        var (buckets, unbucketed) = id switch
        {
            MetricIds.SpendByMonth => await SpendByMonth(months, ct),
            MetricIds.VendorsOnboardedSwecByMonth => await OnboardedByMonth(VendorType.Swec, months, ct),
            MetricIds.VendorsOnboardedNonSwecByMonth => await OnboardedByMonth(VendorType.NonSwec, months, ct),
            _ => throw new NotFoundException($"Metric {id} not found."),
        };
        return new MetricSeriesDto(d.Id, d.Label, d.Unit, buckets, unbucketed);
    }

    private MetricDescriptor Require(string id, bool series)
    {
        var d = Catalog.FirstOrDefault(m => m.Id == id && m.IsSeries == series)
            ?? throw new NotFoundException($"Metric {id} not found.");
        if (!ActionCatalog.RolesFor(d.RequiredAction).Any(user.Roles.Contains))
            throw new ForbiddenException("Not permitted for your role.");
        return d;
    }

    private Guid RequireVendor() =>
        user.VendorId ?? throw new ForbiddenException("This metric is scoped to the vendor principal.");

    // ---- per-principal computes, byte-identical to the retiring DashboardService ----

    private async Task<decimal> TechScoringPending(CancellationToken ct)
    {
        var me = user.UserId ?? "";
        var rfqs = await db.Rfqs.AsNoTracking().ToListAsync(ct);
        return rfqs.Count(r => r.TechnicalEvaluatorIds.Contains(me)
                               && r.Status is RfqStatus.Closed or RfqStatus.Evaluation && !r.TechFinalized);
    }

    private async Task<decimal> CommOpeningsPending(CancellationToken ct)
    {
        var me = user.UserId ?? "";
        var rfqs = await db.Rfqs.AsNoTracking().ToListAsync(ct);
        return rfqs.Count(r => r.CommercialEvaluatorIds.Contains(me)
                               && r.Status == RfqStatus.Evaluation && r.TechFinalized && !r.CommercialOpened);
    }

    private async Task<decimal> AwardsToApprove(CancellationToken ct)
    {
        var me = user.UserId ?? "";
        // [G] SoD: never count awards the caller created — the creator cannot approve.
        return await db.Awards.CountAsync(a => a.Status == Domain.Sourcing.AwardStatus.PendingApproval && a.CreatedByUserId != me, ct);
    }

    private async Task<decimal> VendorRfqsToBid(CancellationToken ct)
    {
        var vid = RequireVendor();
        var invited = (await db.Rfqs.AsNoTracking().Include(r => r.Invitations).ToListAsync(ct))
            .Where(r => r.Invitations.Any(i => i.VendorId == vid && i.Status != RfqInvitationStatus.Rescinded)).ToList();
        var submitted = (await db.Bids.AsNoTracking().Where(b => b.VendorId == vid && b.Submitted).Select(b => b.RfqId).ToListAsync(ct)).ToHashSet();
        return invited.Count(r => r.Status == RfqStatus.Open && !submitted.Contains(r.Id)
                                  && r.Invitations.Any(i => i.VendorId == vid
                                                            && i.Status != RfqInvitationStatus.Declined
                                                            && i.Status != RfqInvitationStatus.Rescinded));
    }

    private async Task<decimal> VendorOpenPos(CancellationToken ct)
    {
        var vid = RequireVendor();
        return await db.PurchaseOrders.CountAsync(p => p.VendorId == vid &&
            (p.Status == PoStatus.Issued || p.Status == PoStatus.Acknowledged || p.Status == PoStatus.PartiallyReceived || p.Status == PoStatus.Received), ct);
    }

    // ---- money/time metrics (honest-null) ----

    private static decimal LineTotal(Invoice i) => i.Lines.Sum(l => l.Qty * l.UnitPrice);

    private (DateOnly start, DateOnly end) Month(int offsetMonths)
    {
        var now = clock.UtcNow.AddMonths(offsetMonths);
        var start = new DateOnly(now.Year, now.Month, 1);
        return (start, start.AddMonths(1));
    }

    private async Task<(decimal value, bool notYet, int excluded)> CommittedSpendMtd(CancellationToken ct)
    {
        var (start, end) = Month(0);
        var invoices = await db.Invoices.AsNoTracking().Include(i => i.Lines).ToListAsync(ct);
        var excluded = invoices.Count(i => i.Date is null);
        var sum = invoices.Where(i => i.Date >= start && i.Date < end).Sum(LineTotal);
        return (sum, false, excluded);   // zero spend this month is a FACT, not absence
    }

    private async Task<(decimal value, bool notYet, int excluded)> SpendVsSameMonthLy(CancellationToken ct)
    {
        var (start, end) = Month(0);
        var (lyStart, lyEnd) = Month(-12);
        var invoices = await db.Invoices.AsNoTracking().Include(i => i.Lines).ToListAsync(ct);
        var excluded = invoices.Count(i => i.Date is null);
        var ly = invoices.Where(i => i.Date >= lyStart && i.Date < lyEnd).Sum(LineTotal);
        if (ly == 0)
            return (0, true, excluded);  // no last-year history → "not yet available", never a fabricated delta
        var now = invoices.Where(i => i.Date >= start && i.Date < end).Sum(LineTotal);
        return (Math.Round((now - ly) / ly * 100, 1), false, excluded);
    }

    private async Task<(decimal value, bool notYet, int excluded)> PrToPoCycleDays(CancellationToken ct)
    {
        // PR.SubmittedUtc → PO.IssuedUtc over the sourcing lineage (PO.RfqId ← PrLineSourcing ← PrLine ← PR).
        var pos = await db.PurchaseOrders.AsNoTracking()
            .Where(p => p.IssuedUtc != null && p.RfqId != null).ToListAsync(ct);
        var issuedTotal = await db.PurchaseOrders.CountAsync(ct);
        var excluded = issuedTotal - pos.Count;                    // POs without IssuedUtc (backfill-null) or RFQ lineage
        if (pos.Count == 0)
            return (0, true, excluded);                            // Slice H posture: null until real flow populates IssuedUtc

        var links = await db.Set<PrLineSourcing>().AsNoTracking()
            .Where(l => l.LinkStatus == LinkStatus.Active).ToListAsync(ct);
        var prs = await db.PurchaseRequisitions.AsNoTracking().Include(p => p.Lines).ToListAsync(ct);
        var prByLine = prs.SelectMany(p => p.Lines.Select(l => (l.Id, p.SubmittedUtc)))
            .ToDictionary(x => x.Id, x => x.SubmittedUtc);

        var samples = new List<double>();
        foreach (var po in pos)
        {
            var submitted = links.Where(l => l.RfqId == po.RfqId)
                .Select(l => prByLine.GetValueOrDefault(l.PrLineId))
                .Where(s => s is not null).Select(s => s!.Value)
                .OrderBy(s => s).Cast<DateTime?>().FirstOrDefault();
            if (submitted is { } s) samples.Add((po.IssuedUtc!.Value - s).TotalDays);
        }
        return samples.Count == 0 ? (0, true, excluded) : (Math.Round((decimal)samples.Average(), 1), false, excluded);
    }

    // ---- series ----

    private List<string> BucketKeys(int months)
    {
        var now = clock.UtcNow;
        var first = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(months - 1));
        return Enumerable.Range(0, months).Select(i => first.AddMonths(i).ToString("yyyy-MM")).ToList();
    }

    private async Task<(IReadOnlyList<SeriesBucketDto>, int)> SpendByMonth(int months, CancellationToken ct)
    {
        var keys = BucketKeys(months);
        var invoices = await db.Invoices.AsNoTracking().Include(i => i.Lines).ToListAsync(ct);
        var unbucketed = invoices.Count(i => i.Date is null);
        var byMonth = invoices.Where(i => i.Date is not null)
            .GroupBy(i => $"{i.Date!.Value.Year:d4}-{i.Date.Value.Month:d2}")
            .ToDictionary(g => g.Key, g => g.Sum(LineTotal));
        return (keys.Select(k => new SeriesBucketDto(k, byMonth.GetValueOrDefault(k, 0m))).ToList(), unbucketed);
    }

    private async Task<(IReadOnlyList<SeriesBucketDto>, int)> OnboardedByMonth(VendorType type, int months, CancellationToken ct)
    {
        var keys = BucketKeys(months);
        var vendors = await db.Vendors.AsNoTracking().Where(v => v.Type == type).ToListAsync(ct);
        var byMonth = vendors.GroupBy(v => v.CreatedUtc.ToString("yyyy-MM"))
            .ToDictionary(g => g.Key, g => (decimal)g.Count());
        return (keys.Select(k => new SeriesBucketDto(k, byMonth.GetValueOrDefault(k, 0m))).ToList(), 0);
    }
}

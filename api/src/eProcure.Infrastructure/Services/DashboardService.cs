using eProcure.Application.Abstractions;
using eProcure.Application.Dashboard;
using eProcure.Domain.Identity;
using eProcure.Domain.Procurement;
using eProcure.Domain.Sourcing;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

/// <summary>Role-aware dashboard metrics — each principal sees the work that needs them.</summary>
public sealed class DashboardService(AppDbContext db, ICurrentUser user) : IDashboardService
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct = default)
    {
        var cards = new List<DashboardCard>();
        var me = user.UserId ?? "";
        var roles = user.Roles;

        // ---- Vendor ----
        if (user.VendorId is { } vid)
        {
            var rfqs = await db.Rfqs.AsNoTracking().Include(r => r.Invitations).ToListAsync(ct);
            var myInvited = rfqs.Where(r => r.Invitations.Any(i => i.VendorId == vid && i.Status != RfqInvitationStatus.Rescinded)).ToList();
            var myBids = await db.Bids.AsNoTracking().Where(b => b.VendorId == vid).ToListAsync(ct);
            var submittedRfqIds = myBids.Where(b => b.Submitted).Select(b => b.RfqId).ToHashSet();
            // "RFQs to bid" = live invitations still awaiting a bid. A Declined invitation is not an
            // outstanding action (Rescinded is already excluded from myInvited); neither inflates the
            // count (BACKLOG / K6a).
            var toBid = myInvited.Count(r => r.Status == RfqStatus.Open
                && !submittedRfqIds.Contains(r.Id)
                && r.Invitations.Any(i => i.VendorId == vid
                    && i.Status != RfqInvitationStatus.Declined
                    && i.Status != RfqInvitationStatus.Rescinded));
            var myPos = await db.PurchaseOrders.AsNoTracking().Where(p => p.VendorId == vid).ToListAsync(ct);

            cards.Add(new("RFQs to bid", toBid.ToString(), "open invitations awaiting your bid", "clay", "dashboard"));
            cards.Add(new("Bids submitted", myBids.Count(b => b.Submitted).ToString(), "in evaluation", "sage", "bids"));
            cards.Add(new("POs to acknowledge", myPos.Count(p => p.Status == PoStatus.Issued && !p.Acknowledged).ToString(), "awaiting your acknowledgement", "velvet", "pos"));
            cards.Add(new("Open purchase orders", myPos.Count(p => p.Status is PoStatus.Issued or PoStatus.Acknowledged or PoStatus.PartiallyReceived or PoStatus.Received).ToString(), "in delivery & billing", "teal", "pos"));

            var name = (await db.Vendors.AsNoTracking().Where(v => v.Id == vid).Select(v => v.Name).FirstOrDefaultAsync(ct)) ?? "your company";
            return new DashboardDto("Vendor Dashboard", $"What needs {name} across sourcing and procure-to-pay.", cards);
        }

        // ---- Internal principals (may hold several roles) ----
        var allRfqs = await db.Rfqs.AsNoTracking().ToListAsync(ct);

        if (roles.Contains(Roles.Buyer) || roles.Contains(Roles.Admin))
        {
            var prs = await db.PurchaseRequisitions.CountAsync(ct);
            cards.Add(new("Open requisitions", prs.ToString(), "ready to source", "clay", "reqs"));
            cards.Add(new("RFQs awaiting bids", allRfqs.Count(r => r.Status == RfqStatus.Open).ToString(), "vendors responding", "amber", "rfqs"));
            cards.Add(new("Ready to open", allRfqs.Count(r => r.Status == RfqStatus.Closed).ToString(), "bid window closed", "sage", "rfqs"));
            cards.Add(new("Under evaluation", allRfqs.Count(r => r.Status == RfqStatus.Evaluation).ToString(), "comparing & negotiating", "velvet", "rfqs"));
        }
        if (roles.Contains(Roles.TechEvaluator))
        {
            var pending = allRfqs.Count(r => r.TechnicalEvaluatorIds.Contains(me)
                && r.Status is RfqStatus.Closed or RfqStatus.Evaluation && !r.TechFinalized);
            cards.Add(new("Technical scoring pending", pending.ToString(), "RFQs assigned to you", "velvet", "openings"));
        }
        if (roles.Contains(Roles.CommEvaluator))
        {
            var pending = allRfqs.Count(r => r.CommercialEvaluatorIds.Contains(me)
                && r.Status == RfqStatus.Evaluation && r.TechFinalized && !r.CommercialOpened);
            cards.Add(new("Commercial envelopes to open", pending.ToString(), "RFQs assigned to you", "clay", "openings"));
        }
        if (roles.Contains(Roles.Approver))
        {
            var pending = await db.Awards.CountAsync(a => a.Status == AwardStatus.PendingApproval && a.CreatedByUserId != me, ct);
            cards.Add(new("Awards to approve", pending.ToString(), "pending your DoA approval", "amber", "awards"));
        }
        if (roles.Contains(Roles.Admin))
        {
            cards.Add(new("Vendors", (await db.Vendors.CountAsync(ct)).ToString(), "registered suppliers", "sage", "vendors"));
            cards.Add(new("Users", (await db.Users.CountAsync(ct)).ToString(), "internal accounts", "teal", "admin"));
        }

        if (cards.Count == 0)
            cards.Add(new("Welcome", "—", "Use “Act as” to switch personas.", "teal", null));

        return new DashboardDto("Sourcing Dashboard", "Live picture of requisitions, active RFQs and awards.", cards);
    }
}

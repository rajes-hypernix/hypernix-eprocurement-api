using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Sourcing;
using eProcure.Domain;
using eProcure.Domain.Identity;
using eProcure.Domain.Procurement;
using eProcure.Domain.Sourcing;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

public sealed class AwardService(
    AppDbContext db,
    IClock clock,
    ICodeGenerator codes,
    IAuditLog audit,
    INetSuiteClient netsuite,
    ICurrentUser user) : IAwardService
{
    // Delegation-of-Authority threshold (configurable). 0 ⇒ every award needs approval.
    private const decimal ApprovalThresholdMyr = 0m;

    public async Task<AwardEligibilityDto?> GetEligibilityAsync(Guid rfqId, CancellationToken ct = default)
    {
        var rfq = await db.Rfqs.AsNoTracking().Include(r => r.Invitations).FirstOrDefaultAsync(r => r.Id == rfqId, ct);
        if (rfq is null) return null;

        // Questionnaire items for the comparison response tables.
        var techQ = rfq.FormItems.Where(i => i.Group == "technical" && i.Kind == "question").OrderBy(i => i.Order)
            .Select(i => new QaItemDto(i.Order, i.Label, i.Type, i.ConfigJson)).ToList();
        var commQ = rfq.FormItems.Where(i => i.Group == "commercial" && i.Kind == "question").OrderBy(i => i.Order)
            .Select(i => new QaItemDto(i.Order, i.Label, i.Type, i.ConfigJson)).ToList();

        var revealed = CommercialRevealed(rfq);
        if (!revealed)
        {
            // [G] sealed bids: never return commercial pricing before the gate.
            var sealedLines = rfq.Lines.Select(l => new AwardLineDto(
                l.ItemCode, l.Description, l.Qty, l.Uom, [], null)).ToList();
            return new AwardEligibilityDto(rfq.Id, rfq.Code, rfq.Title, rfq.Envelope.ToString(), false, rfq.TechFinalized, true, sealedLines, [], techQ, commQ, []);
        }

        var (bids, eligible, scores) = await EligibilityContext(rfq, ct);
        // Blind comparison: vendors are shown as Bidder A/B/C until the award is approved;
        // real identities are revealed only on the approved AwardDto (BUSINESS-RULES [G]).
        string vName(Guid id) => TechnicalEvaluation.Alias(SourcingMapping.LiveInvitedVendorIds(rfq), id);

        var lines = rfq.Lines.Select(l =>
        {
            var opts = eligible
                .Select(vid => (vid, bid: bids[vid].Lines.FirstOrDefault(bl => bl.ItemCode == l.ItemCode)))
                .Where(x => x.bid is { Bidding: true } && x.bid.Price > 0)
                .Select(x => new AwardVendorOptionDto(x.vid, vName(x.vid), x.bid!.Price, x.bid.Qty))
                .OrderBy(o => o.UnitPrice).ToList();
            return new AwardLineDto(l.ItemCode, l.Description, l.Qty, l.Uom, opts, opts.FirstOrDefault()?.VendorId);
        }).ToList();

        // Per-eligible-vendor answers (identities are revealed at award).
        var answerBids = await db.Bids.AsNoTracking().Include(b => b.Answers)
            .Where(b => b.RfqId == rfq.Id && b.Submitted && eligible.Contains(b.VendorId)).ToListAsync(ct);
        var responses = eligible.Select(vid => new AwardResponseDto(vid, vName(vid),
            (answerBids.FirstOrDefault(b => b.VendorId == vid)?.Answers ?? [])
                .Select(a => new QaAnswerDto(a.QuestionOrder, a.Value)).ToList())).ToList();

        var ranking = Ranking(rfq, eligible, bids, scores, vName);
        return new AwardEligibilityDto(rfq.Id, rfq.Code, rfq.Title, rfq.Envelope.ToString(), true, rfq.TechFinalized, true, lines, ranking, techQ, commQ, responses);
    }

    public async Task<AwardDto> SubmitForApprovalAsync(Guid rfqId, SubmitAwardRequest req, CancellationToken ct = default)
    {
        var actor = RequireInternal();
        var rfq = await db.Rfqs.Include(r => r.Invitations).FirstOrDefaultAsync(r => r.Id == rfqId, ct)
            ?? throw new NotFoundException($"RFQ {rfqId} not found.");
        if (!CommercialRevealed(rfq))
            throw new DomainRuleException("Commercial envelope is still sealed — cannot award yet.");

        var (bids, eligible, _) = await EligibilityContext(rfq, ct);

        // Validate every allocation against eligibility + the quantity cap.
        var perLine = req.Allocations.GroupBy(a => a.LineCode);
        var allocations = new List<AwardAllocation>();
        decimal total = 0;
        foreach (var grp in perLine)
        {
            var line = rfq.Lines.FirstOrDefault(l => l.ItemCode == grp.Key)
                ?? throw new DomainRuleException($"Line {grp.Key} is not on this RFQ.");
            decimal lineSum = 0;
            foreach (var a in grp)
            {
                if (a.Qty <= 0) continue;
                if (!eligible.Contains(a.VendorId))
                    throw new DomainRuleException("Cannot award a vendor that did not bid or failed technical evaluation.");
                var bl = bids[a.VendorId].Lines.FirstOrDefault(x => x.ItemCode == grp.Key);
                if (bl is not { Bidding: true } || bl.Price <= 0)
                    throw new DomainRuleException($"Vendor did not bid line {grp.Key}.");

                // [Q] allocatedQty <= min(requiredQty, offeredQty)
                var cap = Math.Min(line.Qty, bl.Qty);
                if (a.Qty > cap)
                    throw new DomainRuleException($"Allocated qty {a.Qty} exceeds the cap {cap} (min of required {line.Qty} and offered {bl.Qty}) for {grp.Key}.");

                lineSum += a.Qty;
                total += a.Qty * bl.Price;
                allocations.Add(new AwardAllocation { RfqLineCode = grp.Key, VendorId = a.VendorId, Qty = a.Qty, UnitPrice = bl.Price });
            }
            if (lineSum > line.Qty)
                throw new DomainRuleException($"Total allocated qty {lineSum} exceeds required {line.Qty} for {grp.Key}.");
        }
        if (allocations.Count == 0)
            throw new DomainRuleException("Allocate at least one line before submitting for approval.");

        var award = await db.Awards.Include(a => a.Allocations).FirstOrDefaultAsync(a => a.RfqId == rfqId, ct);
        if (award is { Status: AwardStatus.Approved })
            throw new DomainRuleException("This RFQ has already been awarded.");
        if (award is null)
        {
            award = new Award { Code = await codes.NextAsync("AWD", ct), RfqId = rfqId, CreatedUtc = clock.UtcNow };
            db.Awards.Add(award);
        }
        award.MarkPendingApproval();    // guards not-already-Approved (service also blocks above)
        award.CreatedByUserId = actor;
        award.Allocations = allocations;
        // TotalValue is computed from Allocations (DBA-10) — no longer stored. `total` is retained
        // locally only for the audit line below.
        award.UpdatedUtc = clock.UtcNow;   // approval stamps reset inside MarkPendingApproval (A2F-T5)
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync("Award", award.Code, "Submitted for approval",
            after: $"RFQ {rfq.Code} · RM {total:N2} · {allocations.Count} allocation(s)", ct: ct);
        return await ToDto(award, ct);
    }

    public async Task<AwardDto> ApproveAsync(Guid awardId, CancellationToken ct = default)
    {
        var approver = RequireInternal();
        var award = await db.Awards.Include(a => a.Allocations).FirstOrDefaultAsync(a => a.Id == awardId, ct)
            ?? throw new NotFoundException($"Award {awardId} not found.");
        if (award.Status != AwardStatus.PendingApproval)
            throw new DomainRuleException($"Award {award.Code} is not pending approval.");

        // [G] Delegation of Authority + segregation of duties.
        if (string.Equals(approver, award.CreatedByUserId, StringComparison.OrdinalIgnoreCase))
            throw new DomainRuleException("The award must be approved by a different person from the one who created it (segregation of duties).");
        if (award.TotalValue >= ApprovalThresholdMyr && !user.Roles.Contains(Roles.Approver))
            throw new DomainRuleException("Award approval requires a user with the Approver (Delegation of Authority) role.");

        var rfq = await db.Rfqs.Include(r => r.Invitations).FirstAsync(r => r.Id == award.RfqId, ct);
        var vendors = await db.Vendors.AsNoTracking().ToListAsync(ct);

        // Generate one PO per awarded vendor.
        var poCodes = new List<string>();
        foreach (var grp in award.Allocations.GroupBy(a => a.VendorId))
        {
            var code = await codes.NextAsync(Domain.Views.RecordType.PurchaseOrder, ct);
            var po = new PurchaseOrder
            {
                Code = code,
                VendorId = grp.Key,
                RfqId = rfq.Id,
                AwardCode = award.Code,
                AwardId = award.Id,                          // AN-2: real lineage to the Award root
                // Status defaults to Draft (the setter is now private).
                NsId = $"NS-PO-{code[^4..]}",
                CreatedUtc = clock.UtcNow,
                UpdatedUtc = clock.UtcNow,
                Lines = grp.Select(a =>
                {
                    var line = rfq.Lines.First(l => l.ItemCode == a.RfqLineCode);
                    // 1:1 by construction — this PO line IS this allocation (Step-0 established, no guesswork).
                    return new PoLine { AwardAllocationId = a.Id, ItemCode = a.RfqLineCode, Description = line.Description, Qty = a.Qty, Uom = line.Uom, UnitPrice = a.UnitPrice };
                }).ToList(),
            };
            db.PurchaseOrders.Add(po);
            await netsuite.PushPurchaseOrderAsync(code, ct); // stub (logs only)
            poCodes.Add(code);
        }

        award.Approve(approver, clock.UtcNow);   // guards PendingApproval; stamps approver/approvedUtc
        rfq.MarkAwarded(clock.UtcNow);
        rfq.UpdatedUtc = clock.UtcNow;

        // [Slice D] Settle PR-line provenance for the awarded RFQ: awarded source lines → Awarded
        // (link stays Active); sourced-but-not-awarded lines → Open with their link Returned
        // ("not awarded / residual"), so the demand is re-sourceable and lineage is preserved
        // (append-only — links are never deleted). No-op for RFQs with no consolidated provenance.
        await SettlePrLinesOnAward(rfq.Id, award.Allocations.Select(a => a.RfqLineCode).ToHashSet(), ct);

        await db.SaveChangesAsync(ct);

        await audit.WriteAsync("Award", award.Code, "Award approved",
            before: $"by {award.CreatedByUserId}", after: $"approved by {approver} · {poCodes.Count} PO(s): {string.Join(", ", poCodes)}", ct: ct);
        foreach (var code in poCodes)
            await audit.WriteAsync("Po", code, "PO generated from award", after: award.Code, ct: ct);
        return await ToDto(award, ct);
    }

    public async Task<AwardDto?> GetForRfqAsync(Guid rfqId, CancellationToken ct = default)
    {
        var award = await db.Awards.AsNoTracking().Include(a => a.Allocations).FirstOrDefaultAsync(a => a.RfqId == rfqId, ct);
        return award is null ? null : await ToDto(award, ct);
    }

    /// <summary>
    /// On award approval, walks every Active sourcing link for the RFQ. If the link's RFQ line was
    /// allocated, the source PR line is marked Awarded (link stays Active); otherwise the link is
    /// closed Returned and the PR line returns to Open for re-sourcing (EDGE-CASES A6/A7/A8).
    /// </summary>
    private async Task SettlePrLinesOnAward(Guid rfqId, HashSet<string> awardedLineCodes, CancellationToken ct)
    {
        var links = await db.PrLineSourcings
            .Where(s => s.RfqId == rfqId && s.LinkStatus == LinkStatus.Active).ToListAsync(ct);
        if (links.Count == 0) return;

        var idSet = links.Select(l => l.PrLineId).ToHashSet();
        var prs = await db.PurchaseRequisitions.Where(p => p.Lines.Any(l => idSet.Contains(l.Id))).ToListAsync(ct);
        var touched = new HashSet<PurchaseRequisition>();
        var now = clock.UtcNow;

        foreach (var link in links)
        {
            var pr = prs.FirstOrDefault(p => p.Lines.Any(l => l.Id == link.PrLineId));
            var line = pr?.Lines.FirstOrDefault(l => l.Id == link.PrLineId);
            if (pr is null || line is null || line.LifecycleStatus != PrLineStatus.InRfq) continue;

            if (awardedLineCodes.Contains(link.RfqLineCode))
            {
                line.MarkAwarded(now);                 // link stays Active (lineage to the PO)
            }
            else
            {
                line.ReturnFromRfq("not awarded / residual", now);
                line.Ref = null;
                link.MarkReturned("not awarded / residual", now);
            }
            touched.Add(pr);
        }

        foreach (var pr in touched) pr.RecomputeHeaderStatus();
    }

    public async Task<IReadOnlyList<AwardDto>> ListAsync(CancellationToken ct = default)
    {
        var awards = await db.Awards.AsNoTracking().Include(a => a.Allocations)
            .OrderByDescending(a => a.CreatedUtc).ToListAsync(ct);
        var list = new List<AwardDto>();
        foreach (var a in awards) list.Add(await ToDto(a, ct));
        return list;
    }

    // ---- helpers ----

    private static bool CommercialRevealed(Rfq rfq) =>
        rfq.Envelope == RfqEnvelope.Single
            ? rfq.Status is RfqStatus.Closed or RfqStatus.Evaluation or RfqStatus.Awarded
            : rfq.CommercialOpened;

    private async Task<(Dictionary<Guid, Bid> Bids, List<Guid> Eligible, List<TechnicalScore> Scores)> EligibilityContext(Rfq rfq, CancellationToken ct)
    {
        var submitted = await db.Bids.AsNoTracking().Include(b => b.Lines)
            .Where(b => b.RfqId == rfq.Id && b.Submitted).ToListAsync(ct);
        var bids = submitted.ToDictionary(b => b.VendorId);
        var scores = await db.TechnicalScores.AsNoTracking().Where(s => s.RfqId == rfq.Id).ToListAsync(ct);

        // Awardable: submitted bidders; for a finalized dual envelope, only TechPass vendors.
        var eligible = SourcingMapping.LiveInvitedVendorIds(rfq)
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
            .Where(g => bids.ContainsKey(g))
            .Where(g => !(rfq.Envelope == RfqEnvelope.Dual && rfq.TechFinalized)
                        || TechnicalEvaluation.Pass(TechnicalEvaluation.Committee(scores, g, rfq.TechnicalEvaluatorIds)))
            .ToList();
        return (bids, eligible, scores);
    }

    private static List<AwardRankRow> Ranking(
        Rfq rfq, List<Guid> eligible, Dictionary<Guid, Bid> bids, List<TechnicalScore> scores, Func<Guid, string> vName)
    {
        decimal TotalFor(Guid v) => rfq.Lines.Sum(l =>
        {
            var bl = bids[v].Lines.FirstOrDefault(x => x.ItemCode == l.ItemCode);
            return bl is { Bidding: true } ? bl.Price * l.Qty : 0;
        });
        var totals = eligible.ToDictionary(v => v, TotalFor);
        var lowest = totals.Values.Where(t => t > 0).DefaultIfEmpty(0).Min();
        var dual = rfq.Envelope == RfqEnvelope.Dual;

        var rows = eligible.Select(v =>
        {
            var priceScore = totals[v] > 0 && lowest > 0 ? Math.Round((double)(lowest / totals[v]) * 1000) / 10 : 0;
            var tech = dual ? TechnicalEvaluation.Committee(scores, v, rfq.TechnicalEvaluatorIds) : null;
            var combined = dual ? Math.Round((0.7 * (tech ?? 0) + 0.3 * priceScore) * 10) / 10 : priceScore;
            return new AwardRankRow(v, vName(v), tech, priceScore, combined, false);
        }).OrderByDescending(r => r.Combined).ToList();

        return rows.Select((r, i) => r with { Recommended = i == 0 }).ToList();
    }

    private string RequireInternal()
    {
        if (user.VendorId is not null || string.IsNullOrEmpty(user.UserId))
            throw new ForbiddenException("Only an internal user can act on awards.");
        return user.UserId!;
    }

    private async Task<AwardDto> ToDto(Award a, CancellationToken ct)
    {
        var rfq = await db.Rfqs.AsNoTracking().FirstAsync(r => r.Id == a.RfqId, ct);
        var vendors = await db.Vendors.AsNoTracking().ToListAsync(ct);
        var poCodes = await db.PurchaseOrders.AsNoTracking().Where(p => p.AwardCode == a.Code)
            .Select(p => p.Code).ToListAsync(ct);
        string vName(Guid id) => vendors.FirstOrDefault(v => v.Id == id)?.Name ?? "Vendor";
        return new AwardDto(a.Id, a.Code, a.RfqId, rfq.Code, a.Status.ToString(), a.CreatedByUserId,
            a.TotalValue, a.ApproverUserId, a.ApprovedUtc,
            a.Allocations.Select(al => new AllocationDto(al.RfqLineCode, al.VendorId, vName(al.VendorId), al.Qty, al.UnitPrice)).ToList(),
            poCodes);
    }
}

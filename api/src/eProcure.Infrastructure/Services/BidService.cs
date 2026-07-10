using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Sourcing;
using eProcure.Domain;
using eProcure.Domain.Sourcing;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

public sealed class BidService(
    AppDbContext db,
    IClock clock,
    ICodeGenerator codes,
    IAuditLog audit,
    ICurrentUser user) : IBidService
{
    public async Task<IReadOnlyList<InvitationDto>> ListMyInvitationsAsync(CancellationToken ct = default)
    {
        var vendorId = RequireVendor();
        var rfqs = await db.Rfqs.AsNoTracking().Include(r => r.Invitations).ToListAsync(ct);
        var invited = rfqs
            .Where(r => r.Invitations.Any(i => i.VendorId == vendorId && i.Status != RfqInvitationStatus.Rescinded))
            .OrderByDescending(r => r.CreatedUtc).ToList();
        var bids = await db.Bids.AsNoTracking().Where(b => b.VendorId == vendorId).ToListAsync(ct);

        // Resolve the buyer-in-charge name so the vendor can address clarifications to them.
        var ownerIds = invited.Select(r => r.OwnerUserId).Where(o => !string.IsNullOrEmpty(o)).Distinct().ToList();
        var ownerNames = await db.Users.AsNoTracking().Where(u => ownerIds.Contains(u.Code))
            .ToDictionaryAsync(u => u.Code, u => u.Name, ct);

        return invited.Select(r =>
        {
            var bid = bids.FirstOrDefault(b => b.RfqId == r.Id);
            var invStatus = r.Invitations.FirstOrDefault(i => i.VendorId == vendorId && i.RoundNumber == r.RoundNumber)?.Status.ToString();
            return new InvitationDto(r.Id, r.Code, r.Title, r.Envelope.ToString(), r.Status.ToString(),
                r.Currency, r.ClosesUtc, bid?.Submitted ?? false, bid?.SavedDraft ?? false,
                r.OwnerUserId, r.OwnerUserId is null ? null : ownerNames.GetValueOrDefault(r.OwnerUserId), invStatus);
        }).ToList();
    }

    public async Task<BidDto?> GetMyBidAsync(Guid rfqId, CancellationToken ct = default)
    {
        var vendorId = RequireVendor();
        var bid = await Find(rfqId, vendorId, ct);
        return bid is null ? null : Map(bid);
    }

    public async Task<BidDto> SaveDraftAsync(Guid rfqId, SaveBidRequest req, CancellationToken ct = default)
    {
        var (rfq, bid) = await LoadForWrite(rfqId, ct);
        EnsureOpen(rfq);

        Apply(bid, req);
        bid.SavedDraft = true;
        bid.Submitted = false;
        bid.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Map(bid);
    }

    public async Task<BidDto> SubmitAsync(Guid rfqId, SaveBidRequest req, CancellationToken ct = default)
    {
        var (rfq, bid) = await LoadForWrite(rfqId, ct);
        EnsureOpen(rfq);

        Apply(bid, req);
        if (!bid.Lines.Any(l => l.Bidding && l.Price > 0))
            throw new DomainRuleException("Enter a price on at least one line before submitting.");

        bid.Submitted = true;
        bid.SavedDraft = false;
        bid.SubmittedUtc = clock.UtcNow;
        bid.WithdrawnUtc = null;          // resubmission clears any prior withdrawal
        bid.UpdatedUtc = clock.UtcNow;
        rfq.RecordBidSubmitted(bid.VendorId, clock.UtcNow);   // invitation → BidSubmitted (T5), same transaction
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync("Bid", bid.Code, "Bid submitted",
            after: $"RFQ {rfq.Code} · {bid.Lines.Count(l => l.Bidding)} line(s)", ct: ct);
        return Map(bid);
    }

    /// <summary>BUSINESS-RULES [G]: a bid may be created/updated/submitted only while
    /// the RFQ is Open and now &lt;= ClosesUtc. Otherwise reject (HTTP 409).</summary>
    private void EnsureOpen(Rfq rfq)
    {
        if (rfq.Status != RfqStatus.Open || (rfq.ClosesUtc is { } close && clock.UtcNow > close))
            throw new DomainRuleException(
                $"Bids have closed for {rfq.Code} — submissions are no longer accepted.");
    }

    private Guid RequireVendor() =>
        user.VendorId ?? throw new ForbiddenException("Only a vendor user can act on bids.");

    private Task<Bid?> Find(Guid rfqId, Guid vendorId, CancellationToken ct) =>
        db.Bids.FirstOrDefaultAsync(b => b.RfqId == rfqId && b.VendorId == vendorId, ct);

    private async Task<(Rfq Rfq, Bid Bid)> LoadForWrite(Guid rfqId, CancellationToken ct)
    {
        var vendorId = RequireVendor();
        var rfq = await db.Rfqs.Include(r => r.Invitations).FirstOrDefaultAsync(r => r.Id == rfqId, ct)
            ?? throw new NotFoundException($"RFQ {rfqId} not found.");

        // Invitation guard with precise semantics (RFQ-LIFECYCLE-ADDENDUM guardrail A):
        //  · no invitation / rescinded → not invited (403, unchanged behaviour)
        //  · declined → must reverse first (409, rule violation — E14)
        var invitation = rfq.Invitations.FirstOrDefault(i => i.VendorId == vendorId && i.RoundNumber == rfq.RoundNumber);
        if (invitation is null || invitation.Status == RfqInvitationStatus.Rescinded)
            throw new ForbiddenException("Your vendor is not invited to this RFQ.");
        if (invitation.Status == RfqInvitationStatus.Declined)
            throw new DomainRuleException("Reverse your decline before bidding on this RFQ.");

        var bid = await Find(rfqId, vendorId, ct);
        if (bid is null)
        {
            bid = new Bid
            {
                Code = await codes.NextAsync("BID", ct),
                RfqId = rfqId,
                VendorId = vendorId,
                CreatedUtc = clock.UtcNow,
                UpdatedUtc = clock.UtcNow,
            };
            db.Bids.Add(bid);
        }
        return (rfq, bid);
    }

    private static void Apply(Bid bid, SaveBidRequest req)
    {
        bid.Lead = req.Lead;
        bid.Warranty = req.Warranty;
        bid.Lines = req.Lines.Select(l => new BidLine
        {
            ItemCode = l.ItemCode, Bidding = l.Bidding, Price = l.Price, Qty = l.Qty,
            Partial = l.Partial, AltItem = l.AltItem,
        }).ToList();
        bid.Answers = req.Answers.Select(a => new BidAnswer { QuestionOrder = a.QuestionOrder, Value = a.Value }).ToList();
        bid.Files = req.Files.Select(f => new BidAttachment { FileName = f }).ToList();
    }

    private static BidDto Map(Bid b) => new(
        b.Id, b.Code, b.RfqId, b.VendorId, b.Submitted, b.SavedDraft, b.SubmittedUtc, b.Lead, b.Warranty,
        b.Lines.Select(l => new BidLineDto(l.ItemCode, l.Bidding, l.Price, l.Qty, l.Partial, l.AltItem)).ToList(),
        b.Answers.Select(a => new BidAnswerDto(a.QuestionOrder, a.Value)).ToList(),
        b.Files.Select(f => f.FileName).ToList());
}

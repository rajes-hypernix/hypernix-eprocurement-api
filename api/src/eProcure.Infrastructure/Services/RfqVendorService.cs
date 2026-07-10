using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Configuration;
using eProcure.Application.Sourcing;
using eProcure.Domain;
using eProcure.Domain.Sourcing;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

/// <summary>
/// Vendor-side RFQ invitation actions (RFQ-LIFECYCLE-ADDENDUM §6). Resource-scoped to the caller's own
/// vendor via ICurrentUser — a vendor can only act on its own invitation. Each action drives the
/// transition through the Rfq aggregate root, writes one RfqEvent + one AuditEntry, in one transaction.
/// </summary>
public sealed class RfqVendorService(
    AppDbContext db,
    IClock clock,
    IAuditLog audit,
    ICurrentUser user,
    ICustomListService customLists) : IRfqVendorService
{
    public async Task DeclineAsync(Guid rfqId, DeclineInvitationRequest req, CancellationToken ct = default)
    {
        await ValidateReasonAsync("RFQ_DECLINE_REASON", req.ReasonCode, req.Note, ct);
        var (rfq, vendorId) = await LoadForVendor(rfqId, ct);
        rfq.DeclineInvitation(vendorId, req.ReasonCode, req.Note, clock.UtcNow);
        db.RfqEvents.Add(RfqEvent.Create(rfqId, RfqEventType.VendorDeclined, clock.UtcNow,
            vendorId: vendorId, actorVendorUserId: user.UserId, reasonCode: req.ReasonCode, reasonNote: req.Note));
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Rfq", rfq.Code, "Vendor declined", after: $"{vendorId} · {req.ReasonCode}", ct: ct);
    }

    public async Task IntendAsync(Guid rfqId, CancellationToken ct = default)
    {
        var (rfq, vendorId) = await LoadForVendor(rfqId, ct);
        var invitation = rfq.Invitations.First(i => i.VendorId == vendorId && i.RoundNumber == rfq.RoundNumber);
        var wasDeclined = invitation.Status == RfqInvitationStatus.Declined;   // T4 reversal vs plain T2

        rfq.DeclareIntendToBid(vendorId, clock.UtcNow);
        // T4 (reversing a decline) is a governance fact; a plain T2 intent is not logged.
        if (wasDeclined)
            db.RfqEvents.Add(RfqEvent.Create(rfqId, RfqEventType.DeclineReversed, clock.UtcNow,
                vendorId: vendorId, actorVendorUserId: user.UserId));
        await db.SaveChangesAsync(ct);
        if (wasDeclined)
            await audit.WriteAsync("Rfq", rfq.Code, "Decline reversed", after: vendorId.ToString(), ct: ct);
    }

    public async Task WithdrawBidAsync(Guid rfqId, CancellationToken ct = default)
    {
        var (rfq, vendorId) = await LoadForVendor(rfqId, ct);
        var bid = await db.Bids.FirstOrDefaultAsync(b => b.RfqId == rfqId && b.VendorId == vendorId, ct)
            ?? throw new NotFoundException("No bid to withdraw.");
        if (!bid.Submitted)
            throw new DomainRuleException("Only a submitted bid can be withdrawn.");

        rfq.WithdrawInvitationBid(vendorId, clock.UtcNow);   // T6 (Open + before close guards)
        bid.Withdraw(clock.UtcNow);
        db.RfqEvents.Add(RfqEvent.Create(rfqId, RfqEventType.BidWithdrawn, clock.UtcNow,
            vendorId: vendorId, actorVendorUserId: user.UserId));
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Bid", bid.Code, "Bid withdrawn", after: $"RFQ {rfq.Code}", ct: ct);
    }

    private async Task<(Rfq Rfq, Guid VendorId)> LoadForVendor(Guid rfqId, CancellationToken ct)
    {
        var vendorId = user.VendorId ?? throw new ForbiddenException("Only a vendor user can act on invitations.");
        var rfq = await db.Rfqs.Include(r => r.Invitations).FirstOrDefaultAsync(r => r.Id == rfqId, ct)
            ?? throw new NotFoundException($"RFQ {rfqId} not found.");
        var invitation = rfq.Invitations.FirstOrDefault(i => i.VendorId == vendorId && i.RoundNumber == rfq.RoundNumber);
        if (invitation is null || invitation.Status == RfqInvitationStatus.Rescinded)
            throw new ForbiddenException("Your vendor is not invited to this RFQ.");
        return (rfq, vendorId);
    }

    private async Task ValidateReasonAsync(string listCode, string reasonCode, string? note, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
            throw new DomainRuleException("A reason code is required.");
        if (note is { Length: > 500 })
            throw new DomainRuleException("The note must be 500 characters or fewer.");
        var list = await customLists.GetAsync(listCode, ct)
            ?? throw new DomainRuleException($"Reason list '{listCode}' is not configured.");
        if (!list.Values.Any(v => v.Code == reasonCode && v.Active))
            throw new DomainRuleException($"'{reasonCode}' is not a valid {list.Name.ToLowerInvariant()}.");
    }
}

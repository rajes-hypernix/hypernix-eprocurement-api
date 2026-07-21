using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Bids;

/// <summary>
/// Loads the RFQ and confirms the caller's vendor was actually invited to it — 404 (never 403)
/// on a mismatch, mirroring <c>Modules.Chat.ChannelAuthorization.RequireMember</c>, so a vendor
/// can't probe whether an RFQ they weren't invited to exists.
/// </summary>
internal static class BidAuthorization
{
    internal static async Task<Rfq> RequireInvitedRfqAsync(SourcingDbContext dbContext, Guid rfqId, Guid vendorId, CancellationToken cancellationToken)
    {
        var rfq = await dbContext.Rfqs
            .Include(r => r.Invitations)
            .FirstOrDefaultAsync(r => r.Id == rfqId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"RFQ {rfqId} not found.");

        // Rescinded invitations are not live — treat like never invited (404, never 403).
        if (!rfq.LiveInvitedVendorIds.Contains(vendorId))
        {
            throw new NotFoundException($"RFQ {rfqId} not found.");
        }

        return rfq;
    }
}

using FSH.Framework.Core.Context;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Bids;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using FSH.Modules.Sourcing.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Bids.ListMyInvitations;

public sealed class ListMyInvitationsQueryHandler(SourcingDbContext dbContext, ICurrentUser currentUser)
    : IQueryHandler<ListMyInvitationsQuery, IReadOnlyList<MyInvitationDto>>
{
    public async ValueTask<IReadOnlyList<MyInvitationDto>> Handle(ListMyInvitationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var vendorId = currentUser.RequireVendorId();

        var invitations = await dbContext.RfqInvitations
            .AsNoTracking()
            .Where(i => i.VendorId == vendorId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (invitations.Count == 0)
        {
            return [];
        }

        var rfqIds = invitations.Select(i => i.RfqId).Distinct().ToList();
        var rfqs = await dbContext.Rfqs
            .AsNoTracking()
            .Where(r => rfqIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken)
            .ConfigureAwait(false);

        var results = new List<MyInvitationDto>();
        foreach (var invitation in invitations)
        {
            if (!rfqs.TryGetValue(invitation.RfqId, out var rfq))
            {
                continue;
            }

            results.Add(new MyInvitationDto(
                rfq.Id,
                rfq.Code,
                rfq.Title,
                rfq.Envelope.ToString(),
                rfq.Status.ToString(),
                invitation.Status.ToString(),
                rfq.OpensUtc,
                rfq.ClosesUtc,
                invitation.Status == RfqInvitationStatus.BidSubmitted));
        }

        return results;
    }
}

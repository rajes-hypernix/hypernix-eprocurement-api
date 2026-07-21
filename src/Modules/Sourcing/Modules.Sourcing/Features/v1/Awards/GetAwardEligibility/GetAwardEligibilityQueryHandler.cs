using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Awards;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Awards.GetAwardEligibility;

public sealed class GetAwardEligibilityQueryHandler(SourcingDbContext dbContext, IMediator mediator)
    : IQueryHandler<GetAwardEligibilityQuery, IReadOnlyList<AwardEligibilityRowDto>>
{
    public async ValueTask<IReadOnlyList<AwardEligibilityRowDto>> Handle(GetAwardEligibilityQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rfq = await dbContext.Rfqs
            .AsNoTracking()
            .Include(r => r.Invitations)
            .FirstOrDefaultAsync(r => r.Id == query.RfqId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"RFQ {query.RfqId} not found.");

        var scores = await dbContext.TechnicalScores
            .AsNoTracking()
            .Where(s => s.RfqId == query.RfqId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        bool masked = !rfq.CommercialRevealed;
        var invitedOrder = rfq.Invitations.OrderBy(i => i.InvitedUtc).Select(i => i.VendorId).ToList();

        var rows = new List<AwardEligibilityRowDto>();
        foreach (var invitation in rfq.Invitations)
        {
            bool submitted = invitation.Status == RfqInvitationStatus.BidSubmitted;
            var committee = TechnicalEvaluation.Committee([.. scores.Where(s => s.VendorId == invitation.VendorId)]);
            bool pass = TechnicalEvaluation.Pass(committee);
            bool eligible = AwardEligibility.IsEligible(rfq, submitted, pass);
            string alias = TechnicalEvaluation.Alias(invitedOrder, invitation.VendorId);

            string? vendorName = null;
            string? vendorCode = null;
            if (!masked)
            {
                try
                {
                    var vendor = await mediator.Send(new GetVendorByIdQuery(invitation.VendorId), cancellationToken).ConfigureAwait(false);
                    vendorName = vendor.Name;
                    vendorCode = vendor.Code;
                }
                catch (NotFoundException)
                {
                    vendorName = "(unknown vendor)";
                }
            }

            rows.Add(new AwardEligibilityRowDto(invitation.VendorId, vendorName, vendorCode, alias, masked, eligible, committee, pass));
        }

        return rows;
    }
}

using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Evaluation;
using FSH.Modules.Sourcing.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Evaluation.GetBidOpening;

public sealed class GetBidOpeningQueryHandler(SourcingDbContext dbContext)
    : IQueryHandler<GetBidOpeningQuery, BidOpeningStatusDto>
{
    public async ValueTask<BidOpeningStatusDto> Handle(GetBidOpeningQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rfq = await dbContext.Rfqs
            .AsNoTracking()
            .Include(r => r.Invitations)
            .FirstOrDefaultAsync(r => r.Id == query.RfqId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"RFQ {query.RfqId} not found.");

        int submittedCount = await dbContext.Bids
            .AsNoTracking()
            .CountAsync(b => b.RfqId == query.RfqId && b.Submitted, cancellationToken)
            .ConfigureAwait(false);

        return new BidOpeningStatusDto(
            rfq.Id,
            rfq.Envelope.ToString(),
            rfq.Status.ToString(),
            rfq.TechnicalOpened,
            rfq.TechFinalized,
            rfq.CommercialOpened,
            rfq.Invitations.Count,
            submittedCount);
    }
}

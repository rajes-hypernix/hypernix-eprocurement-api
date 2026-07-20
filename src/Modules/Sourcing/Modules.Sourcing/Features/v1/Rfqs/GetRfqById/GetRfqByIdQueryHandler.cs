using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.GetRfqById;

public sealed class GetRfqByIdQueryHandler(SourcingDbContext dbContext, IMediator mediator)
    : IQueryHandler<GetRfqByIdQuery, RfqDetailDto>
{
    public async ValueTask<RfqDetailDto> Handle(GetRfqByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rfq = await dbContext.Rfqs
            .AsNoTracking()
            .Include(r => r.Invitations)
            .Include(r => r.Events)
            .FirstOrDefaultAsync(r => r.Id == query.RfqId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"RFQ {query.RfqId} not found.");

        var vendorLookup = new Dictionary<Guid, (string Name, string Code)>();
        foreach (var vendorId in rfq.Invitations.Select(i => i.VendorId).Distinct())
        {
            try
            {
                var vendor = await mediator.Send(new GetVendorByIdQuery(vendorId), cancellationToken).ConfigureAwait(false);
                vendorLookup[vendorId] = (vendor.Name, vendor.Code);
            }
            catch (NotFoundException)
            {
                vendorLookup[vendorId] = ("(unknown vendor)", string.Empty);
            }
        }

        return RfqDtoMapper.ToDetailDto(rfq, vendorLookup);
    }
}

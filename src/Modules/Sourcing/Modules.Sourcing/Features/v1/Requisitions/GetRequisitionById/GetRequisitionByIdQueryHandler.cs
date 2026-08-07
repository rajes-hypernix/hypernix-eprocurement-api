using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Requisitions;
using FSH.Modules.Sourcing.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions.GetRequisitionById;

public sealed class GetRequisitionByIdQueryHandler(SourcingDbContext dbContext, IMediator mediator)
    : IQueryHandler<GetRequisitionByIdQuery, RequisitionDto>
{
    public async ValueTask<RequisitionDto> Handle(GetRequisitionByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pr = await dbContext.PurchaseRequisitions
            .AsNoTracking()
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == query.RequisitionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Requisition {query.RequisitionId} not found.");

        var taxes = await RequisitionWriteSupport.LoadTaxesAsync(mediator, cancellationToken).ConfigureAwait(false);
        var shipTo = await RequisitionWriteSupport.ResolveShipToAsync(mediator, pr, cancellationToken).ConfigureAwait(false);
        return RequisitionDtoMapper.ToDto(pr, taxes, shipTo);
    }
}

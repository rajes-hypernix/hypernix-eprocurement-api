using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using FSH.Modules.Procurement.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.GetPurchaseOrder;

public sealed class GetPurchaseOrderQueryHandler(ProcurementDbContext dbContext)
    : IQueryHandler<GetPurchaseOrderQuery, PurchaseOrderDto?>
{
    public async ValueTask<PurchaseOrderDto?> Handle(GetPurchaseOrderQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var po = await dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == query.PoId, cancellationToken)
            .ConfigureAwait(false);

        return po is null ? null : ProcurementDtoMapper.ToDto(po);
    }
}

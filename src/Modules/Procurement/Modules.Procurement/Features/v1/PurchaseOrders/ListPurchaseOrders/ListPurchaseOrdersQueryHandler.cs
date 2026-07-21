using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using FSH.Modules.Procurement.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.ListPurchaseOrders;

public sealed class ListPurchaseOrdersQueryHandler(ProcurementDbContext dbContext)
    : IQueryHandler<ListPurchaseOrdersQuery, IReadOnlyList<PurchaseOrderListItemDto>>
{
    public async ValueTask<IReadOnlyList<PurchaseOrderListItemDto>> Handle(ListPurchaseOrdersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pos = await dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Lines)
            .OrderByDescending(p => p.CreatedUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. pos.Select(ProcurementDtoMapper.ToListItem)];
    }
}

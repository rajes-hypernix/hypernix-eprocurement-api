using FSH.Framework.Core.Context;
using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Services;
using FSH.Modules.Suppliers.Contracts.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.GetPurchaseOrder;

public sealed class GetPurchaseOrderQueryHandler(
    ProcurementDbContext dbContext,
    IVendorLookupService vendorLookup,
    ICurrentUser currentUser)
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

        if (po is null) return null;
        currentUser.EnsureOwns(po.VendorId);

        var vendors = await vendorLookup.GetManyAsync([po.VendorId], cancellationToken).ConfigureAwait(false);
        return ProcurementDtoMapper.ToDto(po, vendors.TryGetValue(po.VendorId, out var v) ? v.Name : null);
    }
}

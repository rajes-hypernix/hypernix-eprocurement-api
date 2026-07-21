using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Domain;
using FSH.Modules.Procurement.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.AcknowledgePurchaseOrder;

public sealed class AcknowledgePurchaseOrderCommandHandler(ProcurementDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<AcknowledgePurchaseOrderCommand, PurchaseOrderDto>
{
    public async ValueTask<PurchaseOrderDto> Handle(AcknowledgePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vendorId = currentUser.RequireVendorId();

        var po = await dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == command.PoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Purchase order {command.PoId} not found.");

        if (po.VendorId != vendorId)
            throw new NotFoundException($"Purchase order {command.PoId} not found.");

        po.Acknowledge();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ProcurementDtoMapper.ToDto(po);
    }
}

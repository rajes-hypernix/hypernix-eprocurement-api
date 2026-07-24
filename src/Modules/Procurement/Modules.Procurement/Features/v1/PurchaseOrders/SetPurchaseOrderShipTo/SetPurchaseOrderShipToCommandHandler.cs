using FSH.Framework.Core.Exceptions;
using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.SetPurchaseOrderShipTo;

public sealed class SetPurchaseOrderShipToCommandHandler(ProcurementDbContext dbContext)
    : ICommandHandler<SetPurchaseOrderShipToCommand, PurchaseOrderDto>
{
    public async ValueTask<PurchaseOrderDto> Handle(SetPurchaseOrderShipToCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var po = await dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == command.PoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Purchase order {command.PoId} not found.");

        if (command.LocationId is { } locationId && command.AddressId is { } addressId)
        {
            po.SetShipToLocation(locationId, addressId);
        }
        else
        {
            po.SetShipToAdhoc(command.Adhoc);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ProcurementDtoMapper.ToDto(po);
    }
}

using FSH.Framework.Core.Exceptions;
using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using FSH.Modules.Procurement.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.IssuePurchaseOrder;

public sealed class IssuePurchaseOrderCommandHandler(ProcurementDbContext dbContext)
    : ICommandHandler<IssuePurchaseOrderCommand, PurchaseOrderDto>
{
    public async ValueTask<PurchaseOrderDto> Handle(IssuePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var po = await dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == command.PoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Purchase order {command.PoId} not found.");

        po.Issue();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ProcurementDtoMapper.ToDto(po);
    }
}

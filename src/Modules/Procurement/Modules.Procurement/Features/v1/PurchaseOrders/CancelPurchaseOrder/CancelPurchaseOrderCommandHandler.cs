using FSH.Framework.Core.Exceptions;
using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Domain;
using FSH.Modules.Sourcing.Contracts.v1.Requisitions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.CancelPurchaseOrder;

/// <summary>
/// {Draft|Verified} -&gt; Cancelled. When the PO's source is FromRequisition, releases the reserved
/// requisition quantity back via Sourcing after the cancellation is saved — the domain has no way
/// to reach across the module boundary itself.
/// </summary>
public sealed class CancelPurchaseOrderCommandHandler(ProcurementDbContext dbContext, IMediator mediator)
    : ICommandHandler<CancelPurchaseOrderCommand, PurchaseOrderDto>
{
    public async ValueTask<PurchaseOrderDto> Handle(CancelPurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var po = await dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == command.PoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Purchase order {command.PoId} not found.");

        po.Cancel(DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (po.SourceKind == PoSourceKind.FromRequisition)
        {
            await mediator.Send(new ReleaseRequisitionQuantityCommand(po.Id, command.Reason), cancellationToken).ConfigureAwait(false);
        }

        return ProcurementDtoMapper.ToDto(po);
    }
}

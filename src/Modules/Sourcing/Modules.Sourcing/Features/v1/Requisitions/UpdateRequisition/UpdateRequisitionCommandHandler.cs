using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Contracts.v1.Requisitions;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions.UpdateRequisition;

public sealed class UpdateRequisitionCommandHandler(SourcingDbContext dbContext, IMediator mediator)
    : ICommandHandler<UpdateRequisitionCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdateRequisitionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var pr = await dbContext.PurchaseRequisitions
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == command.RequisitionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Requisition {command.RequisitionId} not found.");

        if (command.Lines is { Count: > 0 } lines)
        {
            await RequisitionWriteSupport.EnsureActiveTaxCodesAsync(mediator, lines, cancellationToken).ConfigureAwait(false);
        }

        await RequisitionWriteSupport.EnsureShipToLocationAsync(
            mediator,
            command.ShipToLocationId,
            command.ShipToAddressId,
            command.ShipToAdhoc,
            cancellationToken).ConfigureAwait(false);

        pr.UpdateHeader(
            command.Department,
            command.DepartmentCode,
            command.Location,
            command.LocationCode,
            command.Category,
            command.CategoryCode,
            command.Job,
            command.JobCode,
            command.Memo,
            command.CostCentre,
            command.Project,
            command.RequiredOn,
            command.EntryFormId);

        pr.ApplyShipTo(command.ShipToLocationId, command.ShipToAddressId, command.ShipToAdhoc);

        if (command.Lines is not null)
        {
            pr.SyncOpenLines([.. command.Lines.Select(l =>
                new PrLineDraftInput(l.Id, l.ItemCode, l.Description, l.Qty, l.Uom, l.EstUnitPrice, l.TaxCodeId))]);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return pr.Id;
    }
}

using FSH.Modules.Sourcing.Contracts.v1.Requisitions;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using Mediator;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions.CreateRequisition;

public sealed class CreateRequisitionCommandHandler(SourcingDbContext dbContext, ISourcingCodeGenerator codeGenerator, IMediator mediator)
    : ICommandHandler<CreateRequisitionCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateRequisitionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await RequisitionWriteSupport.EnsureActiveTaxCodesAsync(mediator, command.Lines, cancellationToken).ConfigureAwait(false);
        await RequisitionWriteSupport.EnsureShipToLocationAsync(
            mediator,
            command.ShipToLocationId,
            command.ShipToAddressId,
            command.ShipToAdhoc,
            cancellationToken).ConfigureAwait(false);

        string code = await codeGenerator.NextPurchaseRequisitionCodeAsync(cancellationToken).ConfigureAwait(false);
        var pr = PurchaseRequisition.Create(
            code,
            command.Requestor,
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
            command.EntryFormId,
            command.RaisedOn,
            command.RequiredOn,
            command.Currency);

        pr.ApplyShipTo(command.ShipToLocationId, command.ShipToAddressId, command.ShipToAdhoc);

        foreach (var line in command.Lines)
        {
            pr.AddLine(line.ItemCode, line.Description, line.Qty, line.Uom, line.EstUnitPrice, line.TaxCodeId);
        }

        if (command.Submit)
        {
            pr.Submit(DateTime.UtcNow);
        }

        dbContext.PurchaseRequisitions.Add(pr);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return pr.Id;
    }
}

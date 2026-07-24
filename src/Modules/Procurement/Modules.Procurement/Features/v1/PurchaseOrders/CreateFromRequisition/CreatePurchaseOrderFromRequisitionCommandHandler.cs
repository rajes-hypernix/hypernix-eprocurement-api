using FSH.Framework.Core.Exceptions;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Domain;
using FSH.Modules.Sourcing.Contracts.v1.Requisitions;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;
using Mediator;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.CreateFromRequisition;

/// <summary>
/// PR-direct route: reserve first (Sourcing owns the IC14/IC15 row-lock + cap-check), then create
/// the PO — a failed save releases the reservation so quantity isn't stuck claimed.
/// </summary>
public sealed class CreatePurchaseOrderFromRequisitionCommandHandler(
    ProcurementDbContext dbContext,
    IProcurementCodeGenerator codeGen,
    IMediator mediator)
    : ICommandHandler<CreatePurchaseOrderFromRequisitionCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreatePurchaseOrderFromRequisitionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Lines.Count == 0)
        {
            throw new ProcurementRuleException("At least one line is required to create a purchase order.");
        }

        await mediator.Send(new GetVendorByIdQuery(command.VendorId), cancellationToken).ConfigureAwait(false);

        var pr = await mediator.Send(new GetRequisitionByIdQuery(command.PrId), cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Requisition {command.PrId} not found.");

        string code = await codeGen.NextPoCodeAsync(cancellationToken).ConfigureAwait(false);
        var po = PurchaseOrder.Create(code, command.VendorId, command.Currency, PoSourceKind.FromRequisition, sourcePrId: command.PrId);

        foreach (var input in command.Lines)
        {
            var prLine = pr.Lines.FirstOrDefault(l => l.Id == input.PrLineId)
                ?? throw new NotFoundException($"Requisition line {input.PrLineId} not found on requisition {pr.Code}.");

            if (input.Qty <= 0)
            {
                throw new ProcurementRuleException($"Quantity for {prLine.ItemCode} must be positive.");
            }

            if (input.UnitPrice <= 0)
            {
                throw new ProcurementRuleException($"Unit price for {prLine.ItemCode} must be positive.");
            }

            po.AddLine(
                prLine.ItemCode, prLine.Description, prLine.Uom, input.Qty, input.UnitPrice,
                sourcePrLineId: input.PrLineId, priceConfirmed: input.PriceConfirmed);
        }

        // IC14/IC15: reserve the requested quantity against the requisition BEFORE this PO exists —
        // Sourcing takes a row lock on the PR and caps each line at (requisitioned - already ordered).
        await mediator.Send(
            new ReserveRequisitionQuantityCommand(
                command.PrId,
                po.Id,
                [.. command.Lines.Select(l => new ReserveRequisitionQuantityLineInput(l.PrLineId, l.Qty))]),
            cancellationToken).ConfigureAwait(false);

        try
        {
            dbContext.PurchaseOrders.Add(po);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await mediator.Send(
                new ReleaseRequisitionQuantityCommand(po.Id, "Purchase order save failed after reservation"),
                cancellationToken).ConfigureAwait(false);
            throw;
        }

        return po.Id;
    }
}

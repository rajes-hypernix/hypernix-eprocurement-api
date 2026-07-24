using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Domain;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;
using Mediator;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.CreateStandalone;

/// <summary>Standalone route: no upstream record at all — every field user-entered.</summary>
public sealed class CreateStandalonePurchaseOrderCommandHandler(
    ProcurementDbContext dbContext,
    IProcurementCodeGenerator codeGen,
    IMediator mediator)
    : ICommandHandler<CreateStandalonePurchaseOrderCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateStandalonePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Lines.Count == 0)
        {
            throw new ProcurementRuleException("At least one line is required to create a purchase order.");
        }

        await mediator.Send(new GetVendorByIdQuery(command.VendorId), cancellationToken).ConfigureAwait(false);

        string code = await codeGen.NextPoCodeAsync(cancellationToken).ConfigureAwait(false);
        var po = PurchaseOrder.Create(code, command.VendorId, command.Currency, PoSourceKind.Standalone);

        foreach (var input in command.Lines)
        {
            po.AddLine(input.ItemCode, input.Description, input.Uom, input.Qty, input.UnitPrice);
        }

        dbContext.PurchaseOrders.Add(po);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return po.Id;
    }
}

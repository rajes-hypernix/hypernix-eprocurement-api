using FluentValidation;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.CreateStandalone;

public sealed class CreateStandalonePurchaseOrderCommandValidator : AbstractValidator<CreateStandalonePurchaseOrderCommand>
{
    public CreateStandalonePurchaseOrderCommandValidator()
    {
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.Currency).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemCode).NotEmpty().MaximumLength(50);
            line.RuleFor(l => l.Description).NotEmpty().MaximumLength(500);
            line.RuleFor(l => l.Uom).NotEmpty().MaximumLength(20);
            line.RuleFor(l => l.Qty).GreaterThan(0);
            line.RuleFor(l => l.UnitPrice).GreaterThan(0);
        });
    }
}

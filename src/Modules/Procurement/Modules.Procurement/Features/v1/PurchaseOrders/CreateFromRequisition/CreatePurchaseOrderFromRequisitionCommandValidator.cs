using FluentValidation;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.CreateFromRequisition;

public sealed class CreatePurchaseOrderFromRequisitionCommandValidator : AbstractValidator<CreatePurchaseOrderFromRequisitionCommand>
{
    public CreatePurchaseOrderFromRequisitionCommandValidator()
    {
        RuleFor(x => x.PrId).NotEmpty();
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.Currency).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.PrLineId).NotEmpty();
            line.RuleFor(l => l.Qty).GreaterThan(0);
            line.RuleFor(l => l.UnitPrice).GreaterThan(0);
        });
    }
}

using FluentValidation;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.CreateFromAward;

public sealed class CreatePurchaseOrdersFromAwardCommandValidator : AbstractValidator<CreatePurchaseOrdersFromAwardCommand>
{
    public CreatePurchaseOrdersFromAwardCommandValidator()
    {
        RuleFor(x => x.RfqId).NotEmpty();
    }
}

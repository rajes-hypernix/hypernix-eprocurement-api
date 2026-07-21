using FluentValidation;
using FSH.Modules.Procurement.Contracts.v1.Asns;

namespace FSH.Modules.Procurement.Features.v1.Asns.CreateAsn;

public sealed class CreateAsnCommandValidator : AbstractValidator<CreateAsnCommand>
{
    public CreateAsnCommandValidator()
    {
        RuleFor(x => x.PoId).NotEmpty();
        RuleFor(x => x.Carrier).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TrackingNo).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemCode).NotEmpty().MaximumLength(50);
            line.RuleFor(l => l.ShippedQty).GreaterThan(0);
        });
    }
}

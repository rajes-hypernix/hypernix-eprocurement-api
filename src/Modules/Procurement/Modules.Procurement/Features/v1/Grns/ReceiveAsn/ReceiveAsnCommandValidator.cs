using FluentValidation;
using FSH.Modules.Procurement.Contracts.v1.Grns;

namespace FSH.Modules.Procurement.Features.v1.Grns.ReceiveAsn;

public sealed class ReceiveAsnCommandValidator : AbstractValidator<ReceiveAsnCommand>
{
    public ReceiveAsnCommandValidator()
    {
        RuleFor(x => x.AsnId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemCode).NotEmpty().MaximumLength(50);
            line.RuleFor(l => l.ReceivedQty).GreaterThan(0);
        });
    }
}

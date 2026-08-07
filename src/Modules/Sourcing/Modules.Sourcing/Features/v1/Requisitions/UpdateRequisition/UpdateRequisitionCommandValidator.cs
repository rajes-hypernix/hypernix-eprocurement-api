using FluentValidation;
using FSH.Modules.Sourcing.Contracts.v1.Requisitions;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions.UpdateRequisition;

public sealed class UpdateRequisitionCommandValidator : AbstractValidator<UpdateRequisitionCommand>
{
    public UpdateRequisitionCommandValidator()
    {
        RuleFor(x => x.ShipToAdhoc).MaximumLength(400).When(x => x.ShipToAdhoc is not null);
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemCode).NotEmpty().MaximumLength(50);
            line.RuleFor(l => l.Qty).GreaterThan(0);
        }).When(x => x.Lines is not null);
    }
}

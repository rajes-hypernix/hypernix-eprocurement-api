using FluentValidation;
using FSH.Modules.Sourcing.Contracts.v1.Requisitions;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions.CreateRequisition;

public sealed class CreateRequisitionCommandValidator : AbstractValidator<CreateRequisitionCommand>
{
    public CreateRequisitionCommandValidator()
    {
        RuleFor(x => x.Requestor).NotEmpty().MaximumLength(200);
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemCode).NotEmpty().MaximumLength(50);
            line.RuleFor(l => l.Qty).GreaterThan(0);
        });
    }
}

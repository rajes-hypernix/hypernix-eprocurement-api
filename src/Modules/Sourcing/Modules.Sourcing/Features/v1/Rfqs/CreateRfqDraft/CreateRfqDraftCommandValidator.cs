using FluentValidation;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.CreateRfqDraft;

public sealed class CreateRfqDraftCommandValidator : AbstractValidator<CreateRfqDraftCommand>
{
    public CreateRfqDraftCommandValidator()
    {
        RuleFor(x => x.Envelope).Must(e => e is "Single" or "Dual").WithMessage("Envelope must be 'Single' or 'Dual'.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.LineCode).NotEmpty().MaximumLength(50);
            line.RuleFor(l => l.ItemCode).NotEmpty().MaximumLength(50);
            line.RuleFor(l => l.Qty).GreaterThan(0);
        });
    }
}

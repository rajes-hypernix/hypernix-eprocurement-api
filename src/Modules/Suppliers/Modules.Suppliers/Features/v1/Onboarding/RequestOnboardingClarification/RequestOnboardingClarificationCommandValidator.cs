using FluentValidation;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.RequestOnboardingClarification;

public sealed class RequestOnboardingClarificationCommandValidator : AbstractValidator<RequestOnboardingClarificationCommand>
{
    public RequestOnboardingClarificationCommandValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
        RuleFor(x => x.Message).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Topic).NotEmpty().MaximumLength(300);
            item.RuleFor(i => i.Request).NotEmpty().MaximumLength(2000);
        });
    }
}

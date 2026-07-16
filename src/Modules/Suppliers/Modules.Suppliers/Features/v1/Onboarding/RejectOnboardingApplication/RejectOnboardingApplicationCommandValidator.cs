using FluentValidation;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.RejectOnboardingApplication;

public sealed class RejectOnboardingApplicationCommandValidator : AbstractValidator<RejectOnboardingApplicationCommand>
{
    public RejectOnboardingApplicationCommandValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}

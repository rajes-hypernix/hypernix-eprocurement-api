using FluentValidation;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.CreateOnboardingInvitation;

public sealed class CreateOnboardingInvitationCommandValidator : AbstractValidator<CreateOnboardingInvitationCommand>
{
    public CreateOnboardingInvitationCommandValidator()
    {
        RuleFor(x => x.Type).NotEmpty();
        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

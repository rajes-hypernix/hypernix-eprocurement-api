using FluentValidation;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.SaveOnboardingDraft;

public sealed class SaveOnboardingDraftCommandValidator : AbstractValidator<SaveOnboardingDraftCommand>
{
    public SaveOnboardingDraftCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
    }
}

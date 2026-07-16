using FluentValidation;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.UploadOnboardingDocument;

public sealed class UploadOnboardingDocumentCommandValidator : AbstractValidator<UploadOnboardingDocumentCommand>
{
    public UploadOnboardingDocumentCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Key).NotEmpty().MaximumLength(40);
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(260);
        RuleFor(x => x.Content).NotEmpty();
    }
}

using FSH.Modules.Suppliers.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Suppliers.Contracts.v1.Onboarding;

public sealed record UploadOnboardingDocumentCommand(
    string Token,
    string Key,
    string FileName,
    string ContentType,
    byte[] Content) : ICommand<OnboardingDocumentDto>;

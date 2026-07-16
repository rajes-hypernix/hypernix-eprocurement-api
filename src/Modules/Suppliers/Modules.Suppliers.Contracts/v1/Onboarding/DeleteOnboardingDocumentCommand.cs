using Mediator;

namespace FSH.Modules.Suppliers.Contracts.v1.Onboarding;

public sealed record DeleteOnboardingDocumentCommand(string Token, string Key) : ICommand<Guid>;

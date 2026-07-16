using FSH.Modules.Suppliers.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Suppliers.Contracts.v1.Onboarding;

/// <summary>Anonymous, token-scoped: the vendor opens the magic link.</summary>
public sealed record ResolveOnboardingLinkCommand(string Token) : ICommand<OnboardingApplicationDto>;

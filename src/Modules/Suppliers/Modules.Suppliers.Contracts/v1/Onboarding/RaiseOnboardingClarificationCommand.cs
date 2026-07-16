using FSH.Modules.Suppliers.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Suppliers.Contracts.v1.Onboarding;

/// <summary>Vendor-initiated clarification round back to the buyer — does not change application status.</summary>
public sealed record RaiseOnboardingClarificationCommand(
    string Token,
    string Message,
    IReadOnlyList<ClarificationItemInput> Items) : ICommand<OnboardingApplicationDto>;

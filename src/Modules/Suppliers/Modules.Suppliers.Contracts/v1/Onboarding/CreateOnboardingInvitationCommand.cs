using FSH.Modules.Suppliers.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Suppliers.Contracts.v1.Onboarding;

/// <summary>
/// SelectedTemplateIds are opaque (no Form Template module exists yet — Platform work); they're
/// carried through untouched for a future Platform module to interpret.
/// </summary>
public sealed record CreateOnboardingInvitationCommand(
    string? Email,
    string Type,
    IReadOnlyList<Guid>? SelectedTemplateIds = null) : ICommand<OnboardingInvitationDto>;

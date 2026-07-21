using FSH.Modules.Suppliers.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Suppliers.Contracts.v1.Onboarding;

public sealed record CreateOnboardingInvitationCommand(
    string? Email,
    string Type,
    IReadOnlyList<Guid>? SelectedTemplateIds = null) : ICommand<OnboardingInvitationDto>;

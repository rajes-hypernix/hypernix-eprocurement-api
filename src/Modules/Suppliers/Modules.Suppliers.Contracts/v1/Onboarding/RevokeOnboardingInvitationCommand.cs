using Mediator;

namespace FSH.Modules.Suppliers.Contracts.v1.Onboarding;

/// <summary>Revokes the invite and, if not already terminal, the application it opened.</summary>
public sealed record RevokeOnboardingInvitationCommand(Guid InvitationId) : ICommand<Guid>;

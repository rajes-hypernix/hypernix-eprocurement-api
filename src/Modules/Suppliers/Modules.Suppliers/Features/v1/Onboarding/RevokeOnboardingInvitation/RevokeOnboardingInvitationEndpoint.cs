using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Suppliers.Contracts.Authorization;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.RevokeOnboardingInvitation;

public static class RevokeOnboardingInvitationEndpoint
{
    internal static RouteHandlerBuilder MapRevokeOnboardingInvitationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/onboarding/invitations/{invitationId:guid}/revoke",
                async (Guid invitationId, IMediator mediator, CancellationToken ct) =>
                {
                    await mediator.Send(new RevokeOnboardingInvitationCommand(invitationId), ct).ConfigureAwait(false);
                    return Results.NoContent();
                })
            .WithName("RevokeOnboardingInvitation")
            .WithSummary("Revoke an onboarding invitation")
            .RequirePermission(SuppliersPermissions.Onboarding.Revoke);
    }
}

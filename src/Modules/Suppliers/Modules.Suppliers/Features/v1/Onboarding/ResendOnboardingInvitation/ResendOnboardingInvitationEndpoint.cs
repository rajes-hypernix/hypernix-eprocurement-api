using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Suppliers.Contracts.Authorization;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.ResendOnboardingInvitation;

public static class ResendOnboardingInvitationEndpoint
{
    internal static RouteHandlerBuilder MapResendOnboardingInvitationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/onboarding/invitations/{invitationId:guid}/resend",
                async (Guid invitationId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ResendOnboardingInvitationCommand(invitationId), ct)))
            .WithName("ResendOnboardingInvitation")
            .WithSummary("Resend an onboarding invitation")
            .RequirePermission(SuppliersPermissions.Onboarding.Invite);
    }
}

using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Suppliers.Contracts.Authorization;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.CreateOnboardingInvitation;

public static class CreateOnboardingInvitationEndpoint
{
    internal static RouteHandlerBuilder MapCreateOnboardingInvitationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/onboarding/invitations",
                async (CreateOnboardingInvitationCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("CreateOnboardingInvitation")
            .WithSummary("Invite a vendor to onboard")
            .RequirePermission(SuppliersPermissions.Onboarding.Invite)
            .WithIdempotency();
    }
}

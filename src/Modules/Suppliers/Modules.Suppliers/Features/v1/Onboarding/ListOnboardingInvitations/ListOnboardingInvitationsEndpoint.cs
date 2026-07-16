using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Suppliers.Contracts.Authorization;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.ListOnboardingInvitations;

public static class ListOnboardingInvitationsEndpoint
{
    internal static RouteHandlerBuilder MapListOnboardingInvitationsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/onboarding/invitations",
                (IMediator mediator, CancellationToken ct) => mediator.Send(new ListOnboardingInvitationsQuery(), ct))
            .WithName("ListOnboardingInvitations")
            .WithSummary("List onboarding invitations")
            .RequirePermission(SuppliersPermissions.Onboarding.View);
    }
}

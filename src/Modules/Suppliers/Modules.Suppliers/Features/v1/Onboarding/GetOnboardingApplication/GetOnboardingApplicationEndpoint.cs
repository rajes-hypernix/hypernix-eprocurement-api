using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Suppliers.Contracts.Authorization;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.GetOnboardingApplication;

public static class GetOnboardingApplicationEndpoint
{
    internal static RouteHandlerBuilder MapGetOnboardingApplicationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/onboarding/applications/{applicationId:guid}",
                (Guid applicationId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetOnboardingApplicationQuery(applicationId), ct))
            .WithName("GetOnboardingApplication")
            .WithSummary("Get an onboarding application for buyer review")
            .RequirePermission(SuppliersPermissions.Onboarding.View);
    }
}

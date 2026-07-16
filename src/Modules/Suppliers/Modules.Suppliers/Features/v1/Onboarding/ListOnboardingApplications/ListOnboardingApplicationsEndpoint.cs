using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Suppliers.Contracts.Authorization;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.ListOnboardingApplications;

public static class ListOnboardingApplicationsEndpoint
{
    internal static RouteHandlerBuilder MapListOnboardingApplicationsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/onboarding/applications",
                (IMediator mediator, CancellationToken ct) => mediator.Send(new ListOnboardingApplicationsQuery(), ct))
            .WithName("ListOnboardingApplications")
            .WithSummary("The buyer onboarding queue")
            .RequirePermission(SuppliersPermissions.Onboarding.View);
    }
}

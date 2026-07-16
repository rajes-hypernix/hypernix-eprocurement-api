using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Suppliers.Contracts.Authorization;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.StartOnboardingReview;

public static class StartOnboardingReviewEndpoint
{
    internal static RouteHandlerBuilder MapStartOnboardingReviewEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/onboarding/applications/{applicationId:guid}/start-review",
                async (Guid applicationId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new StartOnboardingReviewCommand(applicationId), ct)))
            .WithName("StartOnboardingReview")
            .WithSummary("Start reviewing an onboarding application")
            .RequirePermission(SuppliersPermissions.Onboarding.Review);
    }
}

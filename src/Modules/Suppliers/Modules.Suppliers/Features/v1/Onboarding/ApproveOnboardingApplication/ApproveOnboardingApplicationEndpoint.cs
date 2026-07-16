using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Suppliers.Contracts.Authorization;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.ApproveOnboardingApplication;

public static class ApproveOnboardingApplicationEndpoint
{
    internal static RouteHandlerBuilder MapApproveOnboardingApplicationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/onboarding/applications/{applicationId:guid}/approve",
                async (Guid applicationId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ApproveOnboardingApplicationCommand(applicationId), ct)))
            .WithName("ApproveOnboardingApplication")
            .WithSummary("Approve an onboarding application, promoting it to the Vendor master")
            .RequirePermission(SuppliersPermissions.Onboarding.Review);
    }
}

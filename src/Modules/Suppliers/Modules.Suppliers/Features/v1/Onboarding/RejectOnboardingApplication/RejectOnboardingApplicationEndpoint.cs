using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Suppliers.Contracts.Authorization;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.RejectOnboardingApplication;

public static class RejectOnboardingApplicationEndpoint
{
    internal static RouteHandlerBuilder MapRejectOnboardingApplicationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/onboarding/applications/{applicationId:guid}/reject",
                async (Guid applicationId, RejectOnboardingApplicationBody body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    return Results.Ok(await mediator.Send(new RejectOnboardingApplicationCommand(applicationId, body.Reason), ct));
                })
            .WithName("RejectOnboardingApplication")
            .WithSummary("Reject an onboarding application")
            .RequirePermission(SuppliersPermissions.Onboarding.Review);
    }
}

public sealed record RejectOnboardingApplicationBody(string Reason);

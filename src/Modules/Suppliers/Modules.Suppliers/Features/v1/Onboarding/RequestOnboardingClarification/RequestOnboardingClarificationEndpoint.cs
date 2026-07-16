using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Suppliers.Contracts.Authorization;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.RequestOnboardingClarification;

public static class RequestOnboardingClarificationEndpoint
{
    internal static RouteHandlerBuilder MapRequestOnboardingClarificationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/onboarding/applications/{applicationId:guid}/clarify",
                async (Guid applicationId, RequestClarificationBody body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    var command = new RequestOnboardingClarificationCommand(applicationId, body.Message, body.Items);
                    return Results.Ok(await mediator.Send(command, ct));
                })
            .WithName("RequestOnboardingClarification")
            .WithSummary("Raise a clarification round with the vendor")
            .RequirePermission(SuppliersPermissions.Onboarding.Review);
    }
}

public sealed record RequestClarificationBody(string Message, IReadOnlyList<ClarificationItemInput> Items);

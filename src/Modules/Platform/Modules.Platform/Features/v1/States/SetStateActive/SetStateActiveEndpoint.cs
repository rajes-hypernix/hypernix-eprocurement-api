using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.States;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.States.SetStateActive;

public static class SetStateActiveEndpoint
{
    internal static RouteHandlerBuilder MapSetStateActiveEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/states/{id:guid}/active",
                async (Guid id, SetStateActiveBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new SetStateActiveCommand(id, body.IsActive), ct);
                    return Results.Ok(result);
                })
            .WithName("SetStateActive")
            .WithSummary("Activate or deactivate a state")
            .RequirePermission(PlatformPermissions.Lookups.Manage);
    }

    private sealed record SetStateActiveBody(bool IsActive);
}

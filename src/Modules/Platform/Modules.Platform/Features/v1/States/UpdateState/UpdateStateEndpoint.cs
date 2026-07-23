using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.States;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.States.UpdateState;

public static class UpdateStateEndpoint
{
    internal static RouteHandlerBuilder MapUpdateStateEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/states/{id:guid}",
                async (Guid id, UpdateStateBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new UpdateStateCommand(id, body.Code, body.Name), ct);
                    return Results.Ok(result);
                })
            .WithName("UpdateState")
            .WithSummary("Update a state")
            .RequirePermission(PlatformPermissions.Lookups.Manage);
    }

    private sealed record UpdateStateBody(string Code, string Name);
}

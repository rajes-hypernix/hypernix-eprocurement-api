using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.States;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.States.CreateState;

public static class CreateStateEndpoint
{
    internal static RouteHandlerBuilder MapCreateStateEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/states",
                async (CreateStateCommand command, IMediator mediator, CancellationToken ct) =>
                {
                    var id = await mediator.Send(command, ct);
                    return Results.Ok(id);
                })
            .WithName("CreateState")
            .WithSummary("Create a state")
            .RequirePermission(PlatformPermissions.Lookups.Manage);
    }
}

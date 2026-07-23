using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Locations.CreateLocation;

public static class CreateLocationEndpoint
{
    internal static RouteHandlerBuilder MapCreateLocationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/locations",
                async (CreateLocationCommand command, IMediator mediator, CancellationToken ct) =>
                {
                    var id = await mediator.Send(command, ct);
                    return Results.Ok(id);
                })
            .WithName("CreateLocation")
            .WithSummary("Create a location")
            .RequirePermission(PlatformPermissions.Configuration.Manage);
    }
}

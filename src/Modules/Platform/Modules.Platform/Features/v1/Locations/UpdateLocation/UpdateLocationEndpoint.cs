using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Locations.UpdateLocation;

public static class UpdateLocationEndpoint
{
    internal static RouteHandlerBuilder MapUpdateLocationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/locations/{id:guid}",
                async (Guid id, UpdateLocationBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new UpdateLocationCommand(id, body.Name, body.Addresses), ct);
                    return Results.Ok(result);
                })
            .WithName("UpdateLocation")
            .WithSummary("Update a location")
            .RequirePermission(PlatformPermissions.Configuration.Manage);
    }

    private sealed record UpdateLocationBody(string Name, IReadOnlyList<LocationAddressInput> Addresses);
}

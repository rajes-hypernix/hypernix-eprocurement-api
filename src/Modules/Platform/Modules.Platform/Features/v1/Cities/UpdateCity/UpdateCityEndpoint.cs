using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Cities;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Cities.UpdateCity;

public static class UpdateCityEndpoint
{
    internal static RouteHandlerBuilder MapUpdateCityEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/cities/{id:guid}",
                async (Guid id, UpdateCityBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new UpdateCityCommand(id, body.Name), ct);
                    return Results.Ok(result);
                })
            .WithName("UpdateCity")
            .WithSummary("Update a city")
            .RequirePermission(PlatformPermissions.Lookups.Manage);
    }

    private sealed record UpdateCityBody(string Name);
}

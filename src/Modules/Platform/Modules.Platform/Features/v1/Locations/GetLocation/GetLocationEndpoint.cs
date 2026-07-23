using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Locations.GetLocation;

public static class GetLocationEndpoint
{
    internal static RouteHandlerBuilder MapGetLocationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/locations/{id:guid}",
                async (Guid id, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new GetLocationQuery(id), ct)))
            .WithName("GetLocation")
            .WithSummary("Get a location with its addresses")
            .RequirePermission(PlatformPermissions.Configuration.View);
    }
}

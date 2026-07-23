using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Locations.ListLocations;

public static class ListLocationsEndpoint
{
    internal static RouteHandlerBuilder MapListLocationsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/locations",
                async (bool? activeOnly, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ListLocationsQuery(activeOnly ?? true), ct)))
            .WithName("ListLocations")
            .WithSummary("List locations")
            .RequirePermission(PlatformPermissions.Configuration.View);
    }
}

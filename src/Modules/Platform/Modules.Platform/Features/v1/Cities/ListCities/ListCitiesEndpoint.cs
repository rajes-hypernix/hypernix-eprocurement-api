using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Cities;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Cities.ListCities;

public static class ListCitiesEndpoint
{
    internal static RouteHandlerBuilder MapListCitiesEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/states/{stateId:guid}/cities",
                async (Guid stateId, bool? activeOnly, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new ListCitiesQuery(stateId, activeOnly ?? true), ct);
                    return Results.Ok(result);
                })
            .WithName("ListCities")
            .WithSummary("List cities for a state")
            .RequirePermission(PlatformPermissions.Lookups.View);
    }
}

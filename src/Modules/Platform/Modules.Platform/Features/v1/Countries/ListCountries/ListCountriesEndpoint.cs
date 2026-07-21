using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Countries;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Countries.ListCountries;

public static class ListCountriesEndpoint
{
    internal static RouteHandlerBuilder MapListCountriesEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/countries",
                async (bool? activeOnly, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new ListCountriesQuery(activeOnly ?? true), ct);
                    return Results.Ok(result);
                })
            .WithName("ListCountries")
            .WithSummary("List countries")
            .RequirePermission(PlatformPermissions.Lookups.View);
    }
}

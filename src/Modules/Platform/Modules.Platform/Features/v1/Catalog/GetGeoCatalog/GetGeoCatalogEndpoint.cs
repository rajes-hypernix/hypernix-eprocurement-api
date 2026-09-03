using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Catalog;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Catalog.GetGeoCatalog;

public static class GetGeoCatalogEndpoint
{
    internal static RouteHandlerBuilder MapGetGeoCatalogEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/catalog/geo",
                async (string? bankCountryCode, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new GetGeoCatalogQuery(
                        string.IsNullOrWhiteSpace(bankCountryCode) ? null : bankCountryCode), ct);
                    return Results.Ok(result);
                })
            .WithName("GetGeoCatalog")
            .WithSummary("Nested country/state/city + banks catalog")
            .RequirePermission(PlatformPermissions.Lookups.View);
    }
}

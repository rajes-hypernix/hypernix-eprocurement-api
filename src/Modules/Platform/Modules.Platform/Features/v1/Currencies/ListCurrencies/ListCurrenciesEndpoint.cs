using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Currencies.ListCurrencies;

public static class ListCurrenciesEndpoint
{
    internal static RouteHandlerBuilder MapListCurrenciesEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/currencies",
                async (bool? activeOnly, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ListCurrenciesQuery(activeOnly ?? true), ct)))
            .WithName("ListCurrencies")
            .WithSummary("List currencies")
            .RequirePermission(PlatformPermissions.Configuration.View);
    }
}

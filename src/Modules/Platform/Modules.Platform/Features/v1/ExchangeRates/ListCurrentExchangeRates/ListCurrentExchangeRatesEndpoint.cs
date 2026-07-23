using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.ExchangeRates.ListCurrentExchangeRates;

public static class ListCurrentExchangeRatesEndpoint
{
    internal static RouteHandlerBuilder MapListCurrentExchangeRatesEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/exchange-rates/current",
                async (IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ListCurrentExchangeRatesQuery(), ct)))
            .WithName("ListCurrentExchangeRates")
            .WithSummary("List the latest exchange rate per active currency")
            .RequirePermission(PlatformPermissions.Configuration.View);
    }
}

using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.ExchangeRates.ListExchangeRateHistory;

public static class ListExchangeRateHistoryEndpoint
{
    internal static RouteHandlerBuilder MapListExchangeRateHistoryEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/exchange-rates/{currencyCode}/history",
                async (string currencyCode, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ListExchangeRateHistoryQuery(currencyCode), ct)))
            .WithName("ListExchangeRateHistory")
            .WithSummary("List exchange rate history for a currency")
            .RequirePermission(PlatformPermissions.Configuration.View);
    }
}

using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.ExchangeRates.AppendExchangeRate;

public static class AppendExchangeRateEndpoint
{
    internal static RouteHandlerBuilder MapAppendExchangeRateEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/exchange-rates",
                async (AppendExchangeRateCommand command, IMediator mediator, CancellationToken ct) =>
                {
                    var id = await mediator.Send(command, ct);
                    return Results.Ok(id);
                })
            .WithName("AppendExchangeRate")
            .WithSummary("Append a new exchange rate entry")
            .RequirePermission(PlatformPermissions.Configuration.Manage);
    }
}

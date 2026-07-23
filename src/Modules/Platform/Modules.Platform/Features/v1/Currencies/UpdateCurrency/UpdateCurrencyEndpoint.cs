using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Currencies.UpdateCurrency;

public static class UpdateCurrencyEndpoint
{
    internal static RouteHandlerBuilder MapUpdateCurrencyEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/currencies/{id:guid}",
                async (Guid id, UpdateCurrencyBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(
                        new UpdateCurrencyCommand(id, body.Name, body.Symbol, body.Decimals),
                        ct);
                    return Results.Ok(result);
                })
            .WithName("UpdateCurrency")
            .WithSummary("Update a currency")
            .RequirePermission(PlatformPermissions.Configuration.Manage);
    }

    private sealed record UpdateCurrencyBody(string Name, string Symbol, int Decimals);
}

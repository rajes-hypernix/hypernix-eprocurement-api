using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Currencies.SetCurrencyActive;

public static class SetCurrencyActiveEndpoint
{
    internal static RouteHandlerBuilder MapSetCurrencyActiveEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/currencies/{id:guid}/active",
                async (Guid id, SetCurrencyActiveBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new SetCurrencyActiveCommand(id, body.IsActive), ct);
                    return Results.Ok(result);
                })
            .WithName("SetCurrencyActive")
            .WithSummary("Activate or deactivate a currency")
            .RequirePermission(PlatformPermissions.Configuration.Manage);
    }

    private sealed record SetCurrencyActiveBody(bool IsActive);
}

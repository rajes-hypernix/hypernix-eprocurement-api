using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Currencies.CreateCurrency;

public static class CreateCurrencyEndpoint
{
    internal static RouteHandlerBuilder MapCreateCurrencyEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/currencies",
                async (CreateCurrencyCommand command, IMediator mediator, CancellationToken ct) =>
                {
                    var id = await mediator.Send(command, ct);
                    return Results.Ok(id);
                })
            .WithName("CreateCurrency")
            .WithSummary("Create a currency")
            .RequirePermission(PlatformPermissions.Configuration.Manage);
    }
}

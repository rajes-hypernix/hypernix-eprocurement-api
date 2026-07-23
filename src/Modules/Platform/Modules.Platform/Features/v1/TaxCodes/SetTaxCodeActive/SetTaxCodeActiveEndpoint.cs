using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.TaxCodes.SetTaxCodeActive;

public static class SetTaxCodeActiveEndpoint
{
    internal static RouteHandlerBuilder MapSetTaxCodeActiveEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/tax-codes/{id:guid}/active",
                async (Guid id, SetTaxCodeActiveBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new SetTaxCodeActiveCommand(id, body.IsActive), ct);
                    return Results.Ok(result);
                })
            .WithName("SetTaxCodeActive")
            .WithSummary("Activate or deactivate a tax code")
            .RequirePermission(PlatformPermissions.Configuration.Manage);
    }

    private sealed record SetTaxCodeActiveBody(bool IsActive);
}

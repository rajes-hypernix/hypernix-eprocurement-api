using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.TaxCodes.UpdateTaxCode;

public static class UpdateTaxCodeEndpoint
{
    internal static RouteHandlerBuilder MapUpdateTaxCodeEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/tax-codes/{id:guid}",
                async (Guid id, UpdateTaxCodeBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new UpdateTaxCodeCommand(id, body.Name, body.RatePct), ct);
                    return Results.Ok(result);
                })
            .WithName("UpdateTaxCode")
            .WithSummary("Update a tax code")
            .RequirePermission(PlatformPermissions.Configuration.Manage);
    }

    private sealed record UpdateTaxCodeBody(string Name, decimal RatePct);
}

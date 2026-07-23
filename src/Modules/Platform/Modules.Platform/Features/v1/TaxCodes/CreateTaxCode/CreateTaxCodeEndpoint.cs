using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.TaxCodes.CreateTaxCode;

public static class CreateTaxCodeEndpoint
{
    internal static RouteHandlerBuilder MapCreateTaxCodeEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/tax-codes",
                async (CreateTaxCodeCommand command, IMediator mediator, CancellationToken ct) =>
                {
                    var id = await mediator.Send(command, ct);
                    return Results.Ok(id);
                })
            .WithName("CreateTaxCode")
            .WithSummary("Create a tax code")
            .RequirePermission(PlatformPermissions.Configuration.Manage);
    }
}

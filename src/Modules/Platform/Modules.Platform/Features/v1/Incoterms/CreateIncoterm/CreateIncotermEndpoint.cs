using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Incoterms.CreateIncoterm;

public static class CreateIncotermEndpoint
{
    internal static RouteHandlerBuilder MapCreateIncotermEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/incoterms",
                async (CreateIncotermCommand command, IMediator mediator, CancellationToken ct) =>
                {
                    var id = await mediator.Send(command, ct);
                    return Results.Ok(id);
                })
            .WithName("CreateIncoterm")
            .WithSummary("Create an incoterm")
            .RequirePermission(PlatformPermissions.Configuration.Manage);
    }
}

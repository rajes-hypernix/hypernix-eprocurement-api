using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Incoterms.UpdateIncoterm;

public static class UpdateIncotermEndpoint
{
    internal static RouteHandlerBuilder MapUpdateIncotermEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/incoterms/{id:guid}",
                async (Guid id, UpdateIncotermBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new UpdateIncotermCommand(id, body.Name), ct);
                    return Results.Ok(result);
                })
            .WithName("UpdateIncoterm")
            .WithSummary("Update an incoterm")
            .RequirePermission(PlatformPermissions.Configuration.Manage);
    }

    private sealed record UpdateIncotermBody(string Name);
}

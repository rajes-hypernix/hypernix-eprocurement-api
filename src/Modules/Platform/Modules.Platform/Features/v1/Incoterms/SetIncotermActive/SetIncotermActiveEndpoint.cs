using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Incoterms.SetIncotermActive;

public static class SetIncotermActiveEndpoint
{
    internal static RouteHandlerBuilder MapSetIncotermActiveEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/incoterms/{id:guid}/active",
                async (Guid id, SetIncotermActiveBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new SetIncotermActiveCommand(id, body.IsActive), ct);
                    return Results.Ok(result);
                })
            .WithName("SetIncotermActive")
            .WithSummary("Activate or deactivate an incoterm")
            .RequirePermission(PlatformPermissions.Configuration.Manage);
    }

    private sealed record SetIncotermActiveBody(bool IsActive);
}

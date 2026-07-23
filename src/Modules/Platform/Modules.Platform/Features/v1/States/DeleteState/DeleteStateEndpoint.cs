using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.States;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.States.DeleteState;

public static class DeleteStateEndpoint
{
    internal static RouteHandlerBuilder MapDeleteStateEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapDelete("/states/{id:guid}",
                async (Guid id, IMediator mediator, CancellationToken ct) =>
                {
                    await mediator.Send(new DeleteStateCommand(id), ct);
                    return Results.NoContent();
                })
            .WithName("DeleteState")
            .WithSummary("Soft-delete a state")
            .RequirePermission(PlatformPermissions.Lookups.Manage);
    }
}

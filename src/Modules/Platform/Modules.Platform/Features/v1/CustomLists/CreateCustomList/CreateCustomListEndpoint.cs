using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.CustomLists;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.CustomLists.CreateCustomList;

public static class CreateCustomListEndpoint
{
    internal static RouteHandlerBuilder MapCreateCustomListEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/custom-lists",
                async (CreateCustomListCommand command, IMediator mediator, CancellationToken ct) =>
                {
                    var id = await mediator.Send(command, ct);
                    return Results.Ok(id);
                })
            .WithName("CreateCustomList")
            .WithSummary("Create a custom list definition")
            .RequirePermission(PlatformPermissions.CustomLists.Manage);
    }
}

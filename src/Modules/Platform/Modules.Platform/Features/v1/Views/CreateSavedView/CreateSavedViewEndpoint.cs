using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Views;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Views.CreateSavedView;

public static class CreateSavedViewEndpoint
{
    internal static RouteHandlerBuilder MapCreateSavedViewEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/views",
                async (SaveViewRequest request, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new CreateSavedViewCommand(request), ct)))
            .WithName("CreateSavedView")
            .WithSummary("Create a saved view owned by the caller")
            .RequirePermission(PlatformPermissions.Views.ManageOwn);
    }
}

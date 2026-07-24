using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.CustomFields;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.CustomFields.SetCustomFieldDefActive;

public static class SetCustomFieldDefActiveEndpoint
{
    internal static RouteHandlerBuilder MapSetCustomFieldDefActiveEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/custom-fields/{id:guid}/active",
                async (Guid id, SetActiveRequest body, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new SetCustomFieldDefActiveCommand(id, body.IsActive), ct)))
            .WithName("SetCustomFieldDefActive")
            .WithSummary("Activate or deactivate a custom field definition")
            .RequirePermission(PlatformPermissions.CustomFields.Manage);
    }
}

public sealed record SetActiveRequest(bool IsActive);

using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.CustomFields;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.CustomFields.UpdateCustomFieldDef;

public static class UpdateCustomFieldDefEndpoint
{
    internal static RouteHandlerBuilder MapUpdateCustomFieldDefEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/custom-fields/{id:guid}",
                async (Guid id, UpdateCustomFieldDefRequest body, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(
                        new UpdateCustomFieldDefCommand(id, body.Label, body.DisplayType, body.ShowInList, body.IsRequired, body.HelpText), ct)))
            .WithName("UpdateCustomFieldDef")
            .WithSummary("Update a custom field definition's non-immutable details")
            .RequirePermission(PlatformPermissions.CustomFields.Manage);
    }
}

public sealed record UpdateCustomFieldDefRequest(string Label, string DisplayType, bool ShowInList, bool IsRequired, string? HelpText);

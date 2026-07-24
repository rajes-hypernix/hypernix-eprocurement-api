using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.CustomFields;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.CustomFields.RemoveCustomFieldApplication;

public static class RemoveCustomFieldApplicationEndpoint
{
    internal static RouteHandlerBuilder MapRemoveCustomFieldApplicationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapDelete("/custom-fields/{id:guid}/apply/{recordType}",
                async (Guid id, string recordType, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new RemoveCustomFieldApplicationCommand(id, recordType), ct)))
            .WithName("RemoveCustomFieldApplication")
            .WithSummary("Remove a custom field definition's application to a record type")
            .RequirePermission(PlatformPermissions.CustomFields.Manage);
    }
}

using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.EntryForms;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.EntryForms.UpdateEntryFormDetails;

public static class UpdateEntryFormDetailsEndpoint
{
    internal static RouteHandlerBuilder MapUpdateEntryFormDetailsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/entry-forms/{id:guid}",
                async (Guid id, UpdateEntryFormDetailsRequest body, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new UpdateEntryFormDetailsCommand(id, body.Name), ct)))
            .WithName("UpdateEntryFormDetails")
            .WithSummary("Rename an entry form")
            .RequirePermission(PlatformPermissions.EntryForms.Manage);
    }
}

public sealed record UpdateEntryFormDetailsRequest(string Name);

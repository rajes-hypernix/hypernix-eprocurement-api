using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.EntryForms;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.EntryForms.SetEntryFormActive;

public static class SetEntryFormActiveEndpoint
{
    internal static RouteHandlerBuilder MapSetEntryFormActiveEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/entry-forms/{id:guid}/active",
                async (Guid id, SetActiveRequest body, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new SetEntryFormActiveCommand(id, body.IsActive), ct)))
            .WithName("SetEntryFormActive")
            .WithSummary("Activate or deactivate an entry form")
            .RequirePermission(PlatformPermissions.EntryForms.Manage);
    }
}

public sealed record SetActiveRequest(bool IsActive);

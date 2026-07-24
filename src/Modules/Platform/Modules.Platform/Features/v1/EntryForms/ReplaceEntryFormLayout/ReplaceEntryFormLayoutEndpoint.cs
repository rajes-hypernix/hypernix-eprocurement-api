using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.EntryForms;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.EntryForms.ReplaceEntryFormLayout;

public static class ReplaceEntryFormLayoutEndpoint
{
    internal static RouteHandlerBuilder MapReplaceEntryFormLayoutEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/entry-forms/{id:guid}/layout",
                async (Guid id, ReplaceEntryFormLayoutRequest body, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ReplaceEntryFormLayoutCommand(id, body.Groups, body.Fields), ct)))
            .WithName("ReplaceEntryFormLayout")
            .WithSummary("Replace an entry form's entire groups+fields layout")
            .RequirePermission(PlatformPermissions.EntryForms.Manage);
    }
}

public sealed record ReplaceEntryFormLayoutRequest(
    IReadOnlyList<EntryFormGroupInputDto> Groups,
    IReadOnlyList<EntryFormFieldInputDto> Fields);

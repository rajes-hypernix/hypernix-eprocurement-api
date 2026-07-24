using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.EntryForms;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.EntryForms.ListEntryFormRoleMaps;

public static class ListEntryFormRoleMapsEndpoint
{
    internal static RouteHandlerBuilder MapListEntryFormRoleMapsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/entry-forms/role-maps", async (string? recordType, IMediator mediator, CancellationToken ct) =>
                Results.Ok(await mediator.Send(new ListEntryFormRoleMapsQuery(recordType), ct)))
            .WithName("ListEntryFormRoleMaps")
            .WithSummary("List (record type, role) -> entry form mappings")
            .RequirePermission(PlatformPermissions.EntryForms.View);
    }
}

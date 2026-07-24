using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.EntryForms;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.EntryForms.UpsertEntryFormRoleMap;

public static class UpsertEntryFormRoleMapEndpoint
{
    internal static RouteHandlerBuilder MapUpsertEntryFormRoleMapEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/entry-forms/role-maps",
                async (UpsertEntryFormRoleMapCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("UpsertEntryFormRoleMap")
            .WithSummary("Point a (record type, role) pair at an entry form")
            .RequirePermission(PlatformPermissions.EntryForms.Manage);
    }
}

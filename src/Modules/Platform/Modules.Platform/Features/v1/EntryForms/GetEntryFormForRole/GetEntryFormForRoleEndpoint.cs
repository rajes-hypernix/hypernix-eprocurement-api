using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.EntryForms;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.EntryForms.GetEntryFormForRole;

public static class GetEntryFormForRoleEndpoint
{
    internal static RouteHandlerBuilder MapGetEntryFormForRoleEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/entry-forms/resolve/{recordType}/{role}",
                async (string recordType, string role, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new GetEntryFormForRoleQuery(recordType, role), ct)))
            .WithName("GetEntryFormForRole")
            .WithSummary("Resolve the entry form a role sees for a record type, with fields fully resolved")
            .RequirePermission(PlatformPermissions.EntryForms.View);
    }
}

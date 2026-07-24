using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.EntryForms;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.EntryForms.ListEntryForms;

public static class ListEntryFormsEndpoint
{
    internal static RouteHandlerBuilder MapListEntryFormsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/entry-forms", async (string? recordType, IMediator mediator, CancellationToken ct) =>
                Results.Ok(await mediator.Send(new ListEntryFormsQuery(recordType), ct)))
            .WithName("ListEntryForms")
            .WithSummary("List entry forms, optionally filtered by record type")
            .RequirePermission(PlatformPermissions.EntryForms.View);
    }
}

using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.EntryForms;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.EntryForms.GetEntryForm;

public static class GetEntryFormEndpoint
{
    internal static RouteHandlerBuilder MapGetEntryFormEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/entry-forms/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
                Results.Ok(await mediator.Send(new GetEntryFormQuery(id), ct)))
            .WithName("GetEntryForm")
            .WithSummary("Get one entry form's full layout")
            .RequirePermission(PlatformPermissions.EntryForms.View);
    }
}

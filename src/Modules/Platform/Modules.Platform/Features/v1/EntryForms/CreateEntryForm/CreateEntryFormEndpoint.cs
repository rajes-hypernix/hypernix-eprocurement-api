using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.EntryForms;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.EntryForms.CreateEntryForm;

public static class CreateEntryFormEndpoint
{
    internal static RouteHandlerBuilder MapCreateEntryFormEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/entry-forms",
                async (CreateEntryFormCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("CreateEntryForm")
            .WithSummary("Create an entry form")
            .RequirePermission(PlatformPermissions.EntryForms.Manage);
    }
}

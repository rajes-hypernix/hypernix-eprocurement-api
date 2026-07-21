using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.FormTemplates;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.FormTemplates.ListFormTemplates;

public static class ListFormTemplatesEndpoint
{
    internal static RouteHandlerBuilder MapListFormTemplatesEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/form-templates",
                async (bool? activeOnly, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ListFormTemplatesQuery(activeOnly ?? true), ct)))
            .WithName("ListFormTemplates")
            .WithSummary("List form templates")
            .RequirePermission(PlatformPermissions.FormTemplates.View);
    }
}

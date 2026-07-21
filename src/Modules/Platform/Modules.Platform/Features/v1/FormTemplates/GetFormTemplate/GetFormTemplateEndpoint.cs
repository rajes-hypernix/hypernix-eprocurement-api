using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.FormTemplates;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.FormTemplates.GetFormTemplate;

public static class GetFormTemplateEndpoint
{
    internal static RouteHandlerBuilder MapGetFormTemplateEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/form-templates/{id:guid}",
                async (Guid id, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new GetFormTemplateQuery(id), ct);
                    return result is null ? Results.NotFound() : Results.Ok(result);
                })
            .WithName("GetFormTemplate")
            .WithSummary("Get a form template with questions")
            .RequirePermission(PlatformPermissions.FormTemplates.View);
    }
}

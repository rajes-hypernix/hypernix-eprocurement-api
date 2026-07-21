using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.FormTemplates;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.FormTemplates.CreateFormTemplate;

public static class CreateFormTemplateEndpoint
{
    internal static RouteHandlerBuilder MapCreateFormTemplateEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/form-templates",
                async (CreateFormTemplateCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("CreateFormTemplate")
            .WithSummary("Create a form template with questions")
            .RequirePermission(PlatformPermissions.FormTemplates.Manage);
    }
}

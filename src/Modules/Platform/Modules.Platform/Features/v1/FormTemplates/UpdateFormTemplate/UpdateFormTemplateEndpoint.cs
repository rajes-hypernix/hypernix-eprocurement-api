using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.FormTemplates;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.FormTemplates.UpdateFormTemplate;

public static class UpdateFormTemplateEndpoint
{
    internal static RouteHandlerBuilder MapUpdateFormTemplateEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/form-templates/{id:guid}",
                async (Guid id, UpdateFormTemplateBody body, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(
                        new UpdateFormTemplateCommand(id, body.Name, body.Questions), ct)))
            .WithName("UpdateFormTemplate")
            .WithSummary("Replace a form template name and questions")
            .RequirePermission(PlatformPermissions.FormTemplates.Manage);
    }
}

public sealed record UpdateFormTemplateBody(
    string Name,
    IReadOnlyList<CreateFormTemplateQuestionDto> Questions);

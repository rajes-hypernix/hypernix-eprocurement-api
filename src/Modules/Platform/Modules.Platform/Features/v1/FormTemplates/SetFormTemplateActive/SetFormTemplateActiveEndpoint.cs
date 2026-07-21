using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.FormTemplates;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.FormTemplates.SetFormTemplateActive;

public static class SetFormTemplateActiveEndpoint
{
    internal static RouteHandlerBuilder MapSetFormTemplateActiveEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/form-templates/{id:guid}/active",
                async (Guid id, SetFormTemplateActiveBody body, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new SetFormTemplateActiveCommand(id, body.IsActive), ct)))
            .WithName("SetFormTemplateActive")
            .WithSummary("Activate or deactivate a form template")
            .RequirePermission(PlatformPermissions.FormTemplates.Manage);
    }

    private sealed record SetFormTemplateActiveBody(bool IsActive);
}

using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.CustomFields;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.CustomFields.CreateCustomFieldDef;

public static class CreateCustomFieldDefEndpoint
{
    internal static RouteHandlerBuilder MapCreateCustomFieldDefEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/custom-fields",
                async (CreateCustomFieldDefCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("CreateCustomFieldDef")
            .WithSummary("Create a custom field definition")
            .RequirePermission(PlatformPermissions.CustomFields.Manage);
    }
}

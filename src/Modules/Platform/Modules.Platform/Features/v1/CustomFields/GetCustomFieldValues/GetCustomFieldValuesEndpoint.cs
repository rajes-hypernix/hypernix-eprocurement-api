using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.CustomFields;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.CustomFields.GetCustomFieldValues;

public static class GetCustomFieldValuesEndpoint
{
    internal static RouteHandlerBuilder MapGetCustomFieldValuesEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/custom-fields/values/{recordType}/{recordId:guid}",
                async (string recordType, Guid recordId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new GetCustomFieldValuesQuery(recordType, recordId), ct)))
            .WithName("GetCustomFieldValues")
            .WithSummary("Get every applicable custom field joined with its value for one record")
            .RequirePermission(PlatformPermissions.CustomFields.View);
    }
}

using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.CustomFields;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.CustomFields.SetCustomFieldValues;

public static class SetCustomFieldValuesEndpoint
{
    internal static RouteHandlerBuilder MapSetCustomFieldValuesEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/custom-fields/values/{recordType}/{recordId:guid}",
                async (string recordType, Guid recordId, IReadOnlyList<CustomFieldValueInput> values, IMediator mediator, CancellationToken ct) =>
                {
                    await mediator.Send(new SetCustomFieldValuesCommand(recordType, recordId, values), ct);
                    return Results.Ok();
                })
            .WithName("SetCustomFieldValues")
            .WithSummary("Upsert custom field values for one record")
            .RequirePermission(PlatformPermissions.CustomFields.View);
    }
}

using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.CustomFields;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.CustomFields.ApplyCustomFieldToRecordType;

public static class ApplyCustomFieldToRecordTypeEndpoint
{
    internal static RouteHandlerBuilder MapApplyCustomFieldToRecordTypeEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/custom-fields/{id:guid}/apply/{recordType}",
                async (Guid id, string recordType, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ApplyCustomFieldToRecordTypeCommand(id, recordType), ct)))
            .WithName("ApplyCustomFieldToRecordType")
            .WithSummary("Apply a custom field definition to a record type")
            .RequirePermission(PlatformPermissions.CustomFields.Manage);
    }
}

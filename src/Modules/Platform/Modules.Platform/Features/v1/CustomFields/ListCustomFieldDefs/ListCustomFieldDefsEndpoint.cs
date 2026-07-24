using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.CustomFields;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.CustomFields.ListCustomFieldDefs;

public static class ListCustomFieldDefsEndpoint
{
    internal static RouteHandlerBuilder MapListCustomFieldDefsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/custom-fields", async (string? recordType, IMediator mediator, CancellationToken ct) =>
                Results.Ok(await mediator.Send(new ListCustomFieldDefsQuery(recordType), ct)))
            .WithName("ListCustomFieldDefs")
            .WithSummary("List custom field definitions, optionally filtered by record type")
            .RequirePermission(PlatformPermissions.CustomFields.View);
    }
}

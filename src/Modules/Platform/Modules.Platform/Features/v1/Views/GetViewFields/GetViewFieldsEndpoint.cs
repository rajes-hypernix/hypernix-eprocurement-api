using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Views;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Views.GetViewFields;

public static class GetViewFieldsEndpoint
{
    internal static RouteHandlerBuilder MapGetViewFieldsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/views/fields",
                async (string recordType, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new GetViewFieldsQuery(recordType), ct)))
            .WithName("GetViewFields")
            .WithSummary("The ViewBuilder's field palette for a record type")
            .RequirePermission(PlatformPermissions.Views.View);
    }
}

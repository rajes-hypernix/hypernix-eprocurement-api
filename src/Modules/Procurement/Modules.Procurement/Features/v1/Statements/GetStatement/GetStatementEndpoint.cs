using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.Statements;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.Statements.GetStatement;

public static class GetStatementEndpoint
{
    internal static RouteHandlerBuilder MapGetStatementEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/statements/{vendorId:guid}",
                async (Guid vendorId, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new GetStatementQuery(vendorId), ct);
                    return result is null ? Results.NotFound() : Results.Ok(result);
                })
            .WithName("GetStatement")
            .WithSummary("Get one vendor's statement of account")
            .RequirePermission(ProcurementPermissions.Statements.View);
    }
}

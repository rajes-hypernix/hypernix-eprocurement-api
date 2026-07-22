using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.Statements;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.Statements.GetMyStatement;

public static class GetMyStatementEndpoint
{
    internal static RouteHandlerBuilder MapGetMyStatementEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/statements/mine",
                async (IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new GetMyStatementQuery(), ct);
                    return Results.Ok(result);
                })
            .WithName("GetMyStatement")
            .WithSummary("Get the current vendor's statement of account")
            .RequirePermission(ProcurementPermissions.Statements.ViewMine);
    }
}

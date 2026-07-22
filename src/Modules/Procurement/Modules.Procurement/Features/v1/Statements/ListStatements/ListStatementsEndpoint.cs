using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.Statements;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.Statements.ListStatements;

public static class ListStatementsEndpoint
{
    internal static RouteHandlerBuilder MapListStatementsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/statements",
                async (IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new ListStatementsQuery(), ct);
                    return Results.Ok(result);
                })
            .WithName("ListStatements")
            .WithSummary("List per-vendor statement summaries (buyer view)")
            .RequirePermission(ProcurementPermissions.Statements.View);
    }
}

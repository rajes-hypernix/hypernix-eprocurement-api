using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.Invoices;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.Invoices.ListInvoices;

public static class ListInvoicesEndpoint
{
    internal static RouteHandlerBuilder MapListInvoicesEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/invoices",
                async (IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new ListInvoicesQuery(), ct);
                    return Results.Ok(result);
                })
            .WithName("ListInvoices")
            .WithSummary("List all invoices")
            .RequirePermission(ProcurementPermissions.Invoices.View);
    }
}

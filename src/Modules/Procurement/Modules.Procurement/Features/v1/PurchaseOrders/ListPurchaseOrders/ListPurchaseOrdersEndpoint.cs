using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.ListPurchaseOrders;

public static class ListPurchaseOrdersEndpoint
{
    internal static RouteHandlerBuilder MapListPurchaseOrdersEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/purchase-orders",
                async (IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new ListPurchaseOrdersQuery(), ct);
                    return Results.Ok(result);
                })
            .WithName("ListPurchaseOrders")
            .WithSummary("List all purchase orders")
            .RequirePermission(ProcurementPermissions.PurchaseOrders.View);
    }
}

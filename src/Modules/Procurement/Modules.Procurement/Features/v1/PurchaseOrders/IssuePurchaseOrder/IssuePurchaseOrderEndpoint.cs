using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.IssuePurchaseOrder;

public static class IssuePurchaseOrderEndpoint
{
    internal static RouteHandlerBuilder MapIssuePurchaseOrderEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/purchase-orders/{poId:guid}/issue",
                async (Guid poId, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new IssuePurchaseOrderCommand(poId), ct);
                    return Results.Ok(result);
                })
            .WithName("IssuePurchaseOrder")
            .WithSummary("Issue a purchase order to the vendor")
            .RequirePermission(ProcurementPermissions.PurchaseOrders.Issue);
    }
}

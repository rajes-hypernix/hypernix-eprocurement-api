using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.CancelPurchaseOrder;

public static class CancelPurchaseOrderEndpoint
{
    internal static RouteHandlerBuilder MapCancelPurchaseOrderEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/purchase-orders/{poId:guid}/cancel",
                async (Guid poId, CancelPurchaseOrderRequest? body, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new CancelPurchaseOrderCommand(poId, body?.Reason), ct)))
            .WithName("CancelPurchaseOrder")
            .WithSummary("Cancel a Draft or Verified purchase order")
            .RequirePermission(ProcurementPermissions.PurchaseOrders.Cancel);
    }
}

public sealed record CancelPurchaseOrderRequest(string? Reason);

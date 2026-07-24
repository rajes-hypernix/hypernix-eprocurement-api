using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.SetPurchaseOrderShipTo;

public static class SetPurchaseOrderShipToEndpoint
{
    internal static RouteHandlerBuilder MapSetPurchaseOrderShipToEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/purchase-orders/{poId:guid}/ship-to",
                async (Guid poId, SetPurchaseOrderShipToRequest body, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(
                        new SetPurchaseOrderShipToCommand(poId, body.LocationId, body.AddressId, body.Adhoc), ct)))
            .WithName("SetPurchaseOrderShipTo")
            .WithSummary("Set a purchase order's ship-to address (Location + address, or ad-hoc text)")
            .RequirePermission(ProcurementPermissions.PurchaseOrders.Issue);
    }
}

public sealed record SetPurchaseOrderShipToRequest(Guid? LocationId, Guid? AddressId, string? Adhoc);

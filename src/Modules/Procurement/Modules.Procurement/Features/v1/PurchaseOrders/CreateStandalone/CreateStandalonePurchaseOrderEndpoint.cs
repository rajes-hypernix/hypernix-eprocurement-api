using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.CreateStandalone;

public static class CreateStandalonePurchaseOrderEndpoint
{
    internal static RouteHandlerBuilder MapCreateStandalonePurchaseOrderEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/purchase-orders/standalone",
                async (CreateStandalonePurchaseOrderCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("CreateStandalonePurchaseOrder")
            .WithSummary("Create a purchase order with no upstream record")
            .RequirePermission(ProcurementPermissions.PurchaseOrders.CreateStandalone);
    }
}

using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.CreateFromAward;

public static class CreatePurchaseOrdersFromAwardEndpoint
{
    internal static RouteHandlerBuilder MapCreatePurchaseOrdersFromAwardEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/purchase-orders/from-award",
                async (CreatePurchaseOrdersFromAwardCommand command, IMediator mediator, CancellationToken ct) =>
                {
                    var ids = await mediator.Send(command, ct);
                    return Results.Ok(ids);
                })
            .WithName("CreatePurchaseOrdersFromAward")
            .WithSummary("Create purchase orders from an approved award")
            .RequirePermission(ProcurementPermissions.PurchaseOrders.CreateFromAward);
    }
}

using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Suppliers.Contracts.Authorization;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Vendors.GetVendorById;

public static class GetVendorByIdEndpoint
{
    internal static RouteHandlerBuilder MapGetVendorByIdEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/vendors/{vendorId:guid}",
                (Guid vendorId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetVendorByIdQuery(vendorId), ct))
            .WithName("GetVendorById")
            .WithSummary("Get a vendor by id")
            .RequirePermission(SuppliersPermissions.Vendors.View);
    }
}

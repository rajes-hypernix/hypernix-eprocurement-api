using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Suppliers.Contracts.Authorization;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Vendors.UpdateVendor;

public static class UpdateVendorEndpoint
{
    internal static RouteHandlerBuilder MapUpdateVendorEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/vendors/{vendorId:guid}",
                async (Guid vendorId, UpdateVendorCommand body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    var command = body with { VendorId = vendorId };
                    return Results.Ok(await mediator.Send(command, ct));
                })
            .WithName("UpdateVendor")
            .WithSummary("Update a vendor")
            .RequirePermission(SuppliersPermissions.Vendors.Update);
    }
}

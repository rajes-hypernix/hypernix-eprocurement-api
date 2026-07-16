using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Suppliers.Contracts.Authorization;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Vendors.ToggleVendorStatus;

public static class ToggleVendorStatusEndpoint
{
    internal static RouteHandlerBuilder MapToggleVendorStatusEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/vendors/{vendorId:guid}/toggle-status",
                async (Guid vendorId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ToggleVendorStatusCommand(vendorId), ct)))
            .WithName("ToggleVendorStatus")
            .WithSummary("Toggle a vendor between Registered and Inactive")
            .RequirePermission(SuppliersPermissions.Vendors.ToggleStatus);
    }
}

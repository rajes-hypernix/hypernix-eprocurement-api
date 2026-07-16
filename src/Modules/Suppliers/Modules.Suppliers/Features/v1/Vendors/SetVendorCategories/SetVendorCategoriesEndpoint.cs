using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Suppliers.Contracts.Authorization;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Vendors.SetVendorCategories;

public static class SetVendorCategoriesEndpoint
{
    internal static RouteHandlerBuilder MapSetVendorCategoriesEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/vendors/{vendorId:guid}/categories",
                async (Guid vendorId, SetVendorCategoriesRequest body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    return Results.Ok(await mediator.Send(new SetVendorCategoriesCommand(vendorId, body.Categories), ct));
                })
            .WithName("SetVendorCategories")
            .WithSummary("Replace a vendor's SWEC category assignments")
            .RequirePermission(SuppliersPermissions.Vendors.Update);
    }
}

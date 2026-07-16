using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Suppliers.Contracts.Authorization;
using FSH.Modules.Suppliers.Contracts.v1.VendorUsers;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.VendorUsers.ListVendorUsers;

public static class ListVendorUsersEndpoint
{
    internal static RouteHandlerBuilder MapListVendorUsersEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/vendors/{vendorId:guid}/users",
                (Guid vendorId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new ListVendorUsersQuery(vendorId), ct))
            .WithName("ListVendorUsers")
            .WithSummary("List a vendor's portal logins")
            .RequirePermission(SuppliersPermissions.VendorUsers.View);
    }
}

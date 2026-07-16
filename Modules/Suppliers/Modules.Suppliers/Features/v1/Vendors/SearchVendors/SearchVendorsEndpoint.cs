using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Suppliers.Contracts.Authorization;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Vendors.SearchVendors;

public static class SearchVendorsEndpoint
{
    internal static RouteHandlerBuilder MapSearchVendorsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/vendors",
                (string? search, int pageNumber, int pageSize, string? sortBy, string? sortDir, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(
                        new SearchVendorsQuery(
                            search,
                            pageNumber == 0 ? 1 : pageNumber,
                            pageSize == 0 ? 20 : pageSize,
                            sortBy,
                            sortDir),
                        ct))
            .WithName("SearchVendors")
            .RequirePermission(SuppliersPermissions.Vendors.View);
    }
}

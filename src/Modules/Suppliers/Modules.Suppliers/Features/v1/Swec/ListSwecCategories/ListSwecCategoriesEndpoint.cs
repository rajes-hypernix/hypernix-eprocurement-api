using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Suppliers.Contracts.Authorization;
using FSH.Modules.Suppliers.Contracts.v1.Swec;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Swec.ListSwecCategories;

public static class ListSwecCategoriesEndpoint
{
    internal static RouteHandlerBuilder MapListSwecCategoriesEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/swec",
                (IMediator mediator, CancellationToken ct) => mediator.Send(new ListSwecCategoriesQuery(), ct))
            .WithName("ListSwecCategories")
            .WithSummary("List the SWEC taxonomy")
            .RequirePermission(SuppliersPermissions.Swec.View);
    }
}

using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Suppliers.Contracts.Authorization;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Vendors.CreateVendor;

public static class CreateVendorEndpoint
{
    internal static RouteHandlerBuilder MapCreateVendorEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/vendors",
                async (CreateVendorCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("CreateVendor")
            .WithSummary("Create a vendor")
            .RequirePermission(SuppliersPermissions.Vendors.Create)
            .WithIdempotency();
    }
}

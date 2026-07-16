using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Suppliers.Contracts.Authorization;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Suppliers.Features.v1.Vendors.CreateManualVendor;

public static class CreateManualVendorEndpoint
{
    internal static RouteHandlerBuilder MapCreateManualVendorEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/vendors/manual",
                async (CreateManualVendorCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("CreateManualVendor")
            .WithSummary("Manually register a vendor (no approval workflow)")
            .RequirePermission(SuppliersPermissions.Vendors.Create)
            .WithIdempotency();
    }
}

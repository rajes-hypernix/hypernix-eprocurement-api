using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Requisitions;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions.CreateRequisition;

public static class CreateRequisitionEndpoint
{
    internal static RouteHandlerBuilder MapCreateRequisitionEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/requisitions",
                async (CreateRequisitionCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("CreateRequisition")
            .WithSummary("Create a purchase requisition")
            .RequirePermission(SourcingPermissions.Requisitions.Manage)
            .WithIdempotency();
    }
}

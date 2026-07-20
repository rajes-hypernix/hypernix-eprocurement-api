using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.GetRfqById;

public static class GetRfqByIdEndpoint
{
    internal static RouteHandlerBuilder MapGetRfqByIdEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/rfqs/{rfqId:guid}",
                (Guid rfqId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetRfqByIdQuery(rfqId), ct))
            .WithName("GetRfqById")
            .WithSummary("Get an RFQ by id")
            .RequirePermission(SourcingPermissions.Rfqs.View);
    }
}

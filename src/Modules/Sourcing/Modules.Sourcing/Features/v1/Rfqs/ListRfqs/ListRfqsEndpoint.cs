using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.ListRfqs;

public static class ListRfqsEndpoint
{
    internal static RouteHandlerBuilder MapListRfqsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/rfqs",
                (IMediator mediator, CancellationToken ct) => mediator.Send(new ListRfqsQuery(), ct))
            .WithName("ListRfqs")
            .WithSummary("List RFQs")
            .RequirePermission(SourcingPermissions.Rfqs.View);
    }
}

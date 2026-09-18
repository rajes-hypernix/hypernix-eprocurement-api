using FSH.Modules.Sourcing.Contracts.v1.Evaluation;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Evaluation.ListBidOpenings;

public static class ListBidOpeningsEndpoint
{
    internal static RouteHandlerBuilder MapListBidOpeningsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/bid-openings",
                (IMediator mediator, CancellationToken ct) => mediator.Send(new ListBidOpeningsQuery(), ct))
            .WithName("ListBidOpenings")
            .WithSummary("List RFQs ready for envelope opening and evaluation")
            .RequireAuthorization();
    }
}

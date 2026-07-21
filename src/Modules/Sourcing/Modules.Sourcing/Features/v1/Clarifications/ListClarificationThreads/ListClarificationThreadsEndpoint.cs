using FSH.Modules.Sourcing.Contracts.v1.Clarifications;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Clarifications.ListClarificationThreads;

public static class ListClarificationThreadsEndpoint
{
    internal static RouteHandlerBuilder MapListClarificationThreadsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/clarifications/threads",
                (IMediator mediator, CancellationToken ct) => mediator.Send(new ListClarificationThreadsQuery(), ct))
            .WithName("ListClarificationThreads")
            .WithSummary("List clarification threads — a vendor sees only their own, an internal caller needs Clarifications.View (checked in-handler).");
    }
}

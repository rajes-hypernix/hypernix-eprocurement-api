using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Segments;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Segments.ListSegmentAssignments;

public static class ListSegmentAssignmentsEndpoint
{
    internal static RouteHandlerBuilder MapListSegmentAssignmentsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/segments/assignments/{recordType}/{recordId:guid}",
                async (string recordType, Guid recordId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ListSegmentAssignmentsQuery(recordType, recordId), ct)))
            .WithName("ListSegmentAssignments")
            .WithSummary("List segment tags assigned to one record")
            .RequirePermission(PlatformPermissions.Org.View);
    }
}

using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Segments;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Segments.RemoveSegmentAssignment;

public static class RemoveSegmentAssignmentEndpoint
{
    internal static RouteHandlerBuilder MapRemoveSegmentAssignmentEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapDelete("/segments/assignments/{recordType}/{recordId:guid}/{dimension}",
                async (string recordType, Guid recordId, string dimension, Guid? lineId, IMediator mediator, CancellationToken ct) =>
                {
                    await mediator.Send(new RemoveSegmentAssignmentCommand(recordType, recordId, lineId, dimension), ct);
                    return Results.Ok();
                })
            .WithName("RemoveSegmentAssignment")
            .WithSummary("Remove a record's segment tag for one dimension")
            .RequirePermission(PlatformPermissions.Org.Manage);
    }
}

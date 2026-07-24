using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Segments;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Segments.SetSegmentAssignment;

public static class SetSegmentAssignmentEndpoint
{
    internal static RouteHandlerBuilder MapSetSegmentAssignmentEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/segments/assignments/{recordType}/{recordId:guid}",
                async (string recordType, Guid recordId, SetSegmentAssignmentRequest body, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(
                        new SetSegmentAssignmentCommand(recordType, recordId, body.LineId, body.Dimension, body.OrgUnitId), ct)))
            .WithName("SetSegmentAssignment")
            .WithSummary("Tag a record (or one of its lines) with an org-unit segment value")
            .RequirePermission(PlatformPermissions.Org.Manage);
    }
}

public sealed record SetSegmentAssignmentRequest(Guid? LineId, string Dimension, Guid OrgUnitId);

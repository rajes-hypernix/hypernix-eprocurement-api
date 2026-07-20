using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.UpdateRfqDraft;

public static class UpdateRfqDraftEndpoint
{
    internal static RouteHandlerBuilder MapUpdateRfqDraftEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/rfqs/{rfqId:guid}",
                async (Guid rfqId, UpdateRfqDraftCommand body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    var command = body with { RfqId = rfqId };
                    return Results.Ok(await mediator.Send(command, ct));
                })
            .WithName("UpdateRfqDraft")
            .WithSummary("Update an RFQ draft")
            .RequirePermission(SourcingPermissions.Rfqs.ManageDraft);
    }
}

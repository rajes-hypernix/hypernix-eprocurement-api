using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.CreateRfqDraft;

public static class CreateRfqDraftEndpoint
{
    internal static RouteHandlerBuilder MapCreateRfqDraftEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/rfqs",
                async (CreateRfqDraftCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("CreateRfqDraft")
            .WithSummary("Create an RFQ draft")
            .RequirePermission(SourcingPermissions.Rfqs.ManageDraft)
            .WithIdempotency();
    }
}

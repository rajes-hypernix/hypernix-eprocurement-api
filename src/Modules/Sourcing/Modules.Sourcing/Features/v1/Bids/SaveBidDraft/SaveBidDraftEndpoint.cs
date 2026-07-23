using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Bids;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Bids.SaveBidDraft;

public static class SaveBidDraftEndpoint
{
    internal static RouteHandlerBuilder MapSaveBidDraftEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/rfqs/{rfqId:guid}/my-bid",
                async (Guid rfqId, SaveBidDraftBody body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    return Results.Ok(await mediator.Send(
                        new SaveBidDraftCommand(rfqId, body.Lead, body.Warranty, body.Lines, body.Answers, body.Files), ct));
                })
            .WithName("SaveBidDraft")
            .WithSummary("Save a draft bid on an RFQ — vendor-portal only.")
            .RequirePermission(SourcingPermissions.Bids.Respond);
    }
}

public sealed record SaveBidDraftBody(
    string? Lead,
    string? Warranty,
    List<BidLineDto> Lines,
    List<BidAnswerDto> Answers,
    List<string> Files);

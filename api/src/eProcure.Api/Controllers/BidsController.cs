using eProcure.Api.Auth;
using eProcure.Application.Authorization;
using eProcure.Application.Sourcing;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Controllers;

/// <summary>
/// Vendor-portal bidding. All actions are scoped to the current vendor principal
/// (X-Demo-User / JWT vendorId) — a vendor can only read/write its own bid.
/// </summary>
[ApiController]
[Route("api/my/rfqs")]
public sealed class MyInvitationsController(IBidService bids) : ControllerBase
{
    [HttpGet]
    [Action(ApiActions.ViewMyInvitations)]
    public async Task<ActionResult<IReadOnlyList<InvitationDto>>> List(CancellationToken ct) =>
        Ok(await bids.ListMyInvitationsAsync(ct));
}

[ApiController]
[Route("api/rfqs/{rfqId:guid}/my-bid")]
public sealed class BidsController(IBidService bids) : ControllerBase
{
    [HttpGet]
    [Action(ApiActions.SubmitBid)]
    public async Task<ActionResult<BidDto>> Get(Guid rfqId, CancellationToken ct)
    {
        var bid = await bids.GetMyBidAsync(rfqId, ct);
        return bid is null ? NoContent() : Ok(bid);
    }

    [HttpPut]
    [Action(ApiActions.SubmitBid)]
    public async Task<ActionResult<BidDto>> SaveDraft(Guid rfqId, [FromBody] SaveBidRequest req, CancellationToken ct) =>
        Ok(await bids.SaveDraftAsync(rfqId, req, ct));

    [HttpPost("submit")]
    [Action(ApiActions.SubmitBid)]
    public async Task<ActionResult<BidDto>> Submit(Guid rfqId, [FromBody] SaveBidRequest req, CancellationToken ct) =>
        Ok(await bids.SubmitAsync(rfqId, req, ct));
}

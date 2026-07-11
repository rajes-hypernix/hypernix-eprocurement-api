using eProcure.Api.Auth;
using eProcure.Application.Authorization;
using eProcure.Application.Sourcing;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Controllers;

/// <summary>
/// Bid opening + technical evaluation. The technical view masks bidder identities
/// for evaluator principals (server-side, bound to the JWT/demo principal) and never
/// returns commercial pricing (sealed until the commercial gate, BUSINESS-RULES [G]).
/// </summary>
[ApiController]
[Route("api/rfqs/{rfqId:guid}")]
public sealed class EvaluationController(IEvaluationService eval) : ControllerBase
{
    [HttpGet("opening")]
    [Action(ApiActions.ViewBidOpenings)]
    public async Task<ActionResult<BidOpeningDto>> Opening(Guid rfqId, CancellationToken ct)
    {
        var dto = await eval.GetOpeningAsync(rfqId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("open-technical")]
    [Action(ApiActions.OpenTechnicalEnvelope)]
    public async Task<ActionResult<BidOpeningDto>> OpenTechnical(Guid rfqId, CancellationToken ct) =>
        Ok(await eval.OpenTechnicalAsync(rfqId, ct));

    [HttpPost("open-commercial")]
    [Action(ApiActions.OpenCommercialEnvelope)]
    public async Task<ActionResult<BidOpeningDto>> OpenCommercial(Guid rfqId, CancellationToken ct) =>
        Ok(await eval.OpenCommercialAsync(rfqId, ct));

    [HttpGet("technical-eval")]
    [Action(ApiActions.ViewTechnicalEval)]
    public async Task<ActionResult<TechnicalEvalDto>> TechnicalEval(Guid rfqId, CancellationToken ct)
    {
        var dto = await eval.GetTechnicalEvalAsync(rfqId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("scores")]
    [Action(ApiActions.ScoreTechnical)]
    public async Task<ActionResult<TechnicalEvalDto>> SetScore(Guid rfqId, [FromBody] SetScoreRequest req, CancellationToken ct) =>
        Ok(await eval.SetScoreAsync(rfqId, req, ct));

    [HttpPost("finalize-technical")]
    [Action(ApiActions.FinalizeTechnical)]
    public async Task<ActionResult<TechnicalEvalDto>> Finalize(Guid rfqId, CancellationToken ct) =>
        Ok(await eval.FinalizeTechnicalAsync(rfqId, ct));
}

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
    public async Task<ActionResult<BidOpeningDto>> Opening(Guid rfqId, CancellationToken ct)
    {
        var dto = await eval.GetOpeningAsync(rfqId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("open-technical")]
    public async Task<ActionResult<BidOpeningDto>> OpenTechnical(Guid rfqId, CancellationToken ct) =>
        Ok(await eval.OpenTechnicalAsync(rfqId, ct));

    [HttpPost("open-commercial")]
    public async Task<ActionResult<BidOpeningDto>> OpenCommercial(Guid rfqId, CancellationToken ct) =>
        Ok(await eval.OpenCommercialAsync(rfqId, ct));

    [HttpGet("technical-eval")]
    public async Task<ActionResult<TechnicalEvalDto>> TechnicalEval(Guid rfqId, CancellationToken ct)
    {
        var dto = await eval.GetTechnicalEvalAsync(rfqId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("scores")]
    public async Task<ActionResult<TechnicalEvalDto>> SetScore(Guid rfqId, [FromBody] SetScoreRequest req, CancellationToken ct) =>
        Ok(await eval.SetScoreAsync(rfqId, req, ct));

    [HttpPost("finalize-technical")]
    public async Task<ActionResult<TechnicalEvalDto>> Finalize(Guid rfqId, CancellationToken ct) =>
        Ok(await eval.FinalizeTechnicalAsync(rfqId, ct));
}

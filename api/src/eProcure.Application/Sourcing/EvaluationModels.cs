namespace eProcure.Application.Sourcing;

public sealed record BidOpeningDto(
    Guid RfqId, string Code, string Title, string Envelope, string Status,
    bool TechnicalOpened, bool CommercialOpened, bool TechFinalized,
    int InvitedCount, int SubmittedCount,
    IReadOnlyList<string> Evaluators,
    bool CanOpenTechnical, bool CanOpenCommercial);

public sealed record CriterionDto(string Key, string Label, int Weight);
public sealed record EvaluatorDto(string Code, string Name);

public sealed record ScoreCellDto(string EvaluatorId, string Criterion, int Score);

public sealed record QaItemDto(int Order, string Label, string Type, string Config);
public sealed record QaAnswerDto(int QuestionOrder, string Value);

public sealed record VendorScoreDto(
    Guid VendorId,
    string DisplayName,            // masked alias for evaluator principals
    IReadOnlyList<ScoreCellDto> Scores,
    double? Committee,
    bool? Pass,
    IReadOnlyList<QaAnswerDto> Answers);

public sealed record TechnicalEvalDto(
    Guid RfqId, string Code, string Title, bool Finalized, int Threshold, bool Masked,
    IReadOnlyList<CriterionDto> Criteria,
    IReadOnlyList<EvaluatorDto> Evaluators,
    IReadOnlyList<VendorScoreDto> Vendors,
    IReadOnlyList<QaItemDto> TechnicalQuestions);

public sealed record SetScoreRequest(Guid VendorId, string EvaluatorId, string Criterion, int Score);

public interface IEvaluationService
{
    Task<BidOpeningDto?> GetOpeningAsync(Guid rfqId, CancellationToken ct = default);
    Task<BidOpeningDto> OpenTechnicalAsync(Guid rfqId, CancellationToken ct = default);
    Task<BidOpeningDto> OpenCommercialAsync(Guid rfqId, CancellationToken ct = default);
    Task<TechnicalEvalDto?> GetTechnicalEvalAsync(Guid rfqId, CancellationToken ct = default);
    Task<TechnicalEvalDto> SetScoreAsync(Guid rfqId, SetScoreRequest req, CancellationToken ct = default);
    Task<TechnicalEvalDto> FinalizeTechnicalAsync(Guid rfqId, CancellationToken ct = default);
}

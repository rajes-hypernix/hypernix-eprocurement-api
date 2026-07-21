namespace FSH.Modules.Sourcing.Contracts.Dtos;

public sealed record TechnicalScoreDto(Guid VendorId, string Criterion, int Score);

public sealed record TechnicalScoreDetailDto(string EvaluatorId, string Criterion, int Score);

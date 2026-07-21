namespace FSH.Modules.Sourcing.Contracts.Dtos;

/// <summary>
/// One vendor's row in the technical-evaluation view. <see cref="Masked"/> callers (pure
/// evaluators, lacking Sourcing.Award.View) get null <see cref="VendorName"/>/<see cref="VendorCode"/>
/// and only see <see cref="Alias"/> — real identity is withheld until the sealed-bid reveal.
/// </summary>
public sealed record TechnicalEvalRowDto(
    Guid VendorId,
    string? VendorName,
    string? VendorCode,
    string Alias,
    bool Masked,
    decimal? CommitteeScore,
    bool Pass,
    IReadOnlyList<TechnicalScoreDetailDto> Scores);

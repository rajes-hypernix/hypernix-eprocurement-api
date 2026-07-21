namespace FSH.Modules.Suppliers.Contracts.Dtos;

/// <summary>
/// Slim application summary used by resolve / start-review / clarify / reject / resubmit.
/// Rounds are included so the anonymous clarification-resubmit UI can render open items.
/// </summary>
public sealed record OnboardingApplicationDto(
    Guid Id,
    string Code,
    string Status,
    string Type,
    string Name,
    string Email,
    DateTime CreatedUtc,
    DateTime? SubmittedUtc,
    IReadOnlyList<Guid> SelectedTemplateIds,
    IReadOnlyList<OnboardingRoundDto> Rounds);

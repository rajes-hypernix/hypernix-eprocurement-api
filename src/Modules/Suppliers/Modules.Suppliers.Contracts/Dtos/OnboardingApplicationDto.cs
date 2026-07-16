namespace FSH.Modules.Suppliers.Contracts.Dtos;

public sealed record OnboardingApplicationDto(
    Guid Id,
    string Code,
    string Status,
    string Type,
    string Name,
    string Email,
    DateTime CreatedUtc,
    DateTime? SubmittedUtc);

namespace FSH.Modules.Suppliers.Contracts.Dtos;

/// <summary>MagicLink is only populated on the Create/Resend response — the raw token is never stored, so it can't be reconstructed later.</summary>
public sealed record OnboardingInvitationDto(
    Guid Id,
    string Email,
    string Type,
    string Status,
    string InvitedByName,
    DateTime CreatedUtc,
    DateTime ExpiresUtc,
    Guid? ApplicationId,
    string? ApplicationCode,
    string? MagicLink);

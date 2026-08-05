namespace FSH.Modules.Suppliers.Contracts.Dtos;

public sealed record OnboardingQueueItemDto(
    Guid Id,
    string Code,
    string Name,
    string Type,
    string Status,
    string Source,
    DateTimeOffset CreatedOnUtc,
    DateTime? SubmittedUtc,
    int? OpenRoundNo,
    int RoundCount,
    Guid? InvitationId);

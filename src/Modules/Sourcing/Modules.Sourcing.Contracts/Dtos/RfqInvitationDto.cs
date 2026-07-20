namespace FSH.Modules.Sourcing.Contracts.Dtos;

public sealed record RfqInvitationDto(
    Guid Id,
    Guid VendorId,
    string? VendorName,
    string? VendorCode,
    int RoundNumber,
    string Status,
    string? DeclineReasonCode,
    string? DeclineNote,
    string? RescindReasonCode,
    string? RescindNote,
    DateTime InvitedUtc,
    DateTime? ViewedUtc,
    DateTime? RespondedUtc,
    DateTime? RescindedUtc);

namespace FSH.Modules.Sourcing.Contracts.Dtos;

public sealed record ClarificationMessageDto(
    Guid Id,
    string Scope,
    Guid VendorId,
    string SenderKind,
    string SenderName,
    string Body,
    bool Published,
    DateTime CreatedUtc,
    bool ReadByBuyer,
    bool ReadByVendor);

public sealed record ClarificationThreadDto(
    string Scope,
    Guid VendorId,
    string? VendorName,
    string? VendorCode,
    int MessageCount,
    DateTime LastMessageUtc,
    bool HasUnread);

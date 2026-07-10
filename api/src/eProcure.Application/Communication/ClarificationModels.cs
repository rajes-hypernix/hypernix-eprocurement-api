namespace eProcure.Application.Communication;

public sealed record ClarificationThreadDto(
    string Scope, Guid VendorId, string VendorName, string ScopeLabel, bool General,
    string LastMessage, DateTime? LastUtc, int Unread);

public sealed record ClarificationMessageDto(
    Guid Id, string SenderKind, string SenderName, string Body, bool Published, DateTime CreatedUtc, bool Mine,
    string? RecipientName);

public sealed record ClarificationThreadDetail(
    string Scope, Guid VendorId, string VendorName, string ScopeLabel, bool General,
    IReadOnlyList<ClarificationMessageDto> Messages);

public sealed record SendClarificationRequest(string Scope, Guid VendorId, string Body, bool Published, string? RecipientUserId = null);

public interface IClarificationService
{
    Task<IReadOnlyList<ClarificationThreadDto>> ListThreadsAsync(CancellationToken ct = default);
    Task<ClarificationThreadDetail?> GetThreadAsync(string scope, Guid vendorId, CancellationToken ct = default);
    Task<ClarificationThreadDetail> SendAsync(SendClarificationRequest req, CancellationToken ct = default);
}

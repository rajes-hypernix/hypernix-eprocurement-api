namespace eProcure.Application.Abstractions;

/// <summary>
/// The authenticated principal for the current request. Internal users are never
/// vendors; a vendor principal carries a <see cref="VendorId"/> used for access
/// scoping (BUSINESS-RULES [G]).
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    string? UserId { get; }
    string? UserName { get; }
    IReadOnlyCollection<string> Roles { get; }

    /// <summary>Set only when the principal is a vendor user; otherwise null.</summary>
    Guid? VendorId { get; }
}

using System.Security.Cryptography;
using System.Text;

namespace eProcure.Domain.Onboarding;

/// <summary>
/// A magic-link onboarding invitation — its own aggregate (grain: one invite). Holds the link
/// token as a SHA-256 <b>hash</b> only (the raw token is NEVER persisted — DATA-MODEL §10, F1),
/// with a 14-day expiry, single-application scope, and its own status. Transitions are methods
/// that throw <see cref="DomainRuleException"/> on an illegal move and return an
/// <see cref="InvitationTransition"/> the caller turns into a typed <c>AuditEntry</c>.
/// </summary>
public class VendorOnboardingInvitation
{
    /// <summary>Default magic-link validity (SPEC §1.2).</summary>
    public const int DefaultValidityDays = 14;

    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Email { get; private set; } = default!;
    public Suppliers.VendorType Type { get; private set; }

    /// <summary>Selected onboarding <c>FormTemplate</c> ids for this invite (SPEC §5).</summary>
    public List<Guid> SelectedTemplateIds { get; private set; } = [];

    /// <summary>SHA-256 hex of the raw token. The raw token exists only in the emailed link.</summary>
    public string TokenHash { get; private set; } = default!;

    public OnboardingInvitationStatus Status { get; private set; } = OnboardingInvitationStatus.Sent;

    public string InvitedByUserId { get; private set; } = default!;
    public string InvitedByName { get; private set; } = default!;

    /// <summary>The application this link opens/created (set when the vendor first opens it).</summary>
    public Guid? ApplicationId { get; private set; }

    public DateTime CreatedUtc { get; private set; }
    public DateTime ExpiresUtc { get; private set; }
    public DateTime? OpenedUtc { get; private set; }
    public DateTime? RevokedUtc { get; private set; }

    // EF Core
    private VendorOnboardingInvitation() { }

    /// <summary>
    /// Creates an invitation from a freshly-generated <paramref name="rawToken"/> (supplied by the
    /// infrastructure RNG — Slice B). The raw token is hashed immediately and discarded; only the
    /// hash is retained. Status starts <see cref="OnboardingInvitationStatus.Sent"/>.
    /// </summary>
    public static VendorOnboardingInvitation Create(string email, Suppliers.VendorType type,
        IEnumerable<Guid> selectedTemplateIds, string rawToken, string invitedByUserId,
        string invitedByName, DateTime nowUtc, int validityDays = DefaultValidityDays) => new()
    {
        Email = email,
        Type = type,
        SelectedTemplateIds = [.. selectedTemplateIds],
        TokenHash = HashToken(rawToken),
        Status = OnboardingInvitationStatus.Sent,
        InvitedByUserId = invitedByUserId,
        InvitedByName = invitedByName,
        CreatedUtc = nowUtc,
        ExpiresUtc = nowUtc.AddDays(validityDays),
    };

    /// <summary>Links the staging application created for this invite (set once, at invite time — A1).</summary>
    public void AttachApplication(Guid applicationId)
    {
        if (ApplicationId is not null && ApplicationId != applicationId)
            throw new DomainRuleException("This invitation is already linked to an application.");
        ApplicationId = applicationId;
    }

    /// <summary>
    /// Reissues the link with a fresh token and expiry (A9 — buyer resends after expiry). Any live or
    /// expired invite can be resent; a completed one cannot. Status returns to
    /// <see cref="OnboardingInvitationStatus.Sent"/>.
    /// </summary>
    public InvitationTransition Reissue(string rawToken, DateTime nowUtc, int validityDays = DefaultValidityDays)
    {
        if (Status is OnboardingInvitationStatus.Completed or OnboardingInvitationStatus.Revoked)
            throw new DomainRuleException($"A {Status} onboarding invitation cannot be resent.");
        var from = Status;
        TokenHash = HashToken(rawToken);
        ExpiresUtc = nowUtc.AddDays(validityDays);
        Status = OnboardingInvitationStatus.Sent;
        OpenedUtc = null;
        RevokedUtc = null;
        return new InvitationTransition(from, Status, "Onboarding invitation resent", null);
    }

    /// <summary>SHA-256 hex digest used for the stored token hash and for verification.</summary>
    public static string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();

    /// <summary>Whether a presented raw token matches this invite's stored hash (constant-time compare).</summary>
    public bool Matches(string rawToken) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(TokenHash), Encoding.UTF8.GetBytes(HashToken(rawToken)));

    public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresUtc;

    /// <summary>
    /// Vendor opens the link (A2). Requires the token to match and the invite to be live
    /// (<see cref="OnboardingInvitationStatus.Sent"/>, not expired/revoked). Sent → Opened; links
    /// the application. Idempotent once Opened for the same token.
    /// </summary>
    public InvitationTransition Open(string rawToken, Guid applicationId, DateTime nowUtc)
    {
        if (!Matches(rawToken))
            throw new DomainRuleException("The onboarding link is not valid.");
        if (Status is OnboardingInvitationStatus.Revoked)
            throw new DomainRuleException("This onboarding invitation has been revoked.");
        if (Status is OnboardingInvitationStatus.Expired || IsExpired(nowUtc))
            throw new DomainRuleException("This onboarding link has expired.");
        if (Status is OnboardingInvitationStatus.Completed)
            throw new DomainRuleException("This onboarding invitation is already completed.");

        var from = Status;
        ApplicationId = applicationId;
        if (Status == OnboardingInvitationStatus.Sent)
        {
            Status = OnboardingInvitationStatus.Opened;
            OpenedUtc = nowUtc;
        }
        return new InvitationTransition(from, Status, "Onboarding link opened", null);
    }

    /// <summary>Buyer revokes the invite (A10). The token no longer resolves. Sent/Opened → Revoked.</summary>
    public InvitationTransition Revoke(DateTime nowUtc)
    {
        RequireLive("revoke");
        var from = Status;
        Status = OnboardingInvitationStatus.Revoked;
        RevokedUtc = nowUtc;
        return new InvitationTransition(from, Status, "Onboarding invitation revoked", null);
    }

    /// <summary>Link lapses before use (A9). Sent/Opened → Expired.</summary>
    public InvitationTransition Expire(DateTime nowUtc)
    {
        RequireLive("expire");
        var from = Status;
        Status = OnboardingInvitationStatus.Expired;
        return new InvitationTransition(from, Status, "Onboarding link expired", null);
    }

    /// <summary>Marks the invite fulfilled once its application is approved. → Completed.</summary>
    public InvitationTransition Complete(DateTime nowUtc)
    {
        if (Status is OnboardingInvitationStatus.Revoked or OnboardingInvitationStatus.Expired)
            throw new DomainRuleException($"A {Status} invitation cannot be completed.");
        var from = Status;
        Status = OnboardingInvitationStatus.Completed;
        return new InvitationTransition(from, Status, "Onboarding invitation completed", null);
    }

    private void RequireLive(string action)
    {
        if (Status is not (OnboardingInvitationStatus.Sent or OnboardingInvitationStatus.Opened))
            throw new DomainRuleException($"Cannot {action} a {Status} onboarding invitation.");
    }
}

/// <summary>The recorded result of an invitation transition — the single signal a service turns
/// into an <c>AuditEntry</c> (typed from/to columns) for onboarding-throughput analytics.</summary>
public sealed record InvitationTransition(
    OnboardingInvitationStatus From, OnboardingInvitationStatus To, string Action, string? Reason);

using System.Security.Cryptography;
using System.Text;
using FSH.Framework.Core.Domain;
using FSH.Modules.Suppliers.Domain;

namespace FSH.Modules.Suppliers.Domain.Onboarding;

/// <summary>
/// A magic-link onboarding invitation — its own aggregate (grain: one invite). Holds the link
/// token as a SHA-256 hash only (the raw token is never persisted), with a 14-day expiry,
/// single-application scope, and its own status.
/// </summary>
public sealed class VendorOnboardingInvitation : AggregateRoot<Guid>, IAuditableEntity
{
    public const int DefaultValidityDays = 14;

    public string Email { get; private set; } = default!;
    public VendorType Type { get; private set; } = VendorType.NonSwec;

    public List<Guid> SelectedTemplateIds { get; private set; } = [];

    /// <summary>SHA-256 hex of the raw token. The raw token exists only in the emailed link.</summary>
    public string TokenHash { get; private set; } = default!;

    public OnboardingInvitationStatus Status { get; private set; } = OnboardingInvitationStatus.Sent;
    public string InvitedByUserId { get; private set; } = default!;
    public string InvitedByName { get; private set; } = default!;

    /// <summary>The application this link opens/created (set when the vendor first opens it).</summary>
    public Guid? ApplicationId { get; private set; }

    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOnUtc { get; private set; }
    public string? LastModifiedBy { get; private set; }
    public DateTime ExpiresUtc { get; private set; }
    public DateTime? OpenedUtc { get; private set; }
    public DateTime? RevokedUtc { get; private set; }

    private VendorOnboardingInvitation() { }

    public static VendorOnboardingInvitation Create(
        string email,
        VendorType type,
        IEnumerable<Guid> selectedTemplateIds,
        string rawToken,
        string invitedByUserId,
        string invitedByName,
        DateTime nowUtc,
        int validityDays = DefaultValidityDays)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var invitation = new VendorOnboardingInvitation
        {
            Id = Guid.CreateVersion7(),
            Email = email.Trim(),
            Type = type,
            TokenHash = HashToken(rawToken),
            Status = OnboardingInvitationStatus.Sent,
            InvitedByUserId = invitedByUserId,
            InvitedByName = invitedByName,
            CreatedOnUtc = AuditTime.FromUtc(nowUtc),
            CreatedBy = null,
            ExpiresUtc = nowUtc.AddDays(validityDays),
        };
        invitation.SelectedTemplateIds.AddRange(selectedTemplateIds);
        return invitation;
    }

    /// <summary>Links the staging application created for this invite (set once, at invite time).</summary>
    public void AttachApplication(Guid applicationId)
    {
        if (ApplicationId is not null && ApplicationId != applicationId)
        {
            throw new OnboardingRuleException("This invitation is already linked to an application.");
        }

        ApplicationId = applicationId;
        Touch();
    }

    private void Touch(DateTime? nowUtc = null)
    {
        LastModifiedOnUtc = nowUtc is { } stamp ? AuditTime.FromUtc(stamp) : AuditTime.UtcNow;
        LastModifiedBy = null;
    }

    /// <summary>
    /// Reissues the link with a fresh token and expiry. Any live or expired invite can be resent;
    /// a completed one cannot. Status returns to <see cref="OnboardingInvitationStatus.Sent"/>.
    /// </summary>
    public InvitationTransition Reissue(string rawToken, DateTime nowUtc, int validityDays = DefaultValidityDays)
    {
        if (Status is OnboardingInvitationStatus.Completed or OnboardingInvitationStatus.Revoked)
        {
            throw new OnboardingRuleException($"A {Status} onboarding invitation cannot be resent.");
        }

        var from = Status;
        TokenHash = HashToken(rawToken);
        ExpiresUtc = nowUtc.AddDays(validityDays);
        Status = OnboardingInvitationStatus.Sent;
        OpenedUtc = null;
        RevokedUtc = null;
        Touch(nowUtc);
        return new InvitationTransition(from, Status, "Onboarding invitation resent", null);
    }

    public static string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();

    /// <summary>Whether a presented raw token matches this invite's stored hash (constant-time compare).</summary>
    public bool Matches(string rawToken) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(TokenHash), Encoding.UTF8.GetBytes(HashToken(rawToken)));

    public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresUtc;

    /// <summary>
    /// Vendor opens the link. Requires the token to match and the invite to be live (Sent, not
    /// expired/revoked). Sent -&gt; Opened; links the application. Idempotent once Opened for the same token.
    /// </summary>
    public InvitationTransition Open(string rawToken, Guid applicationId, DateTime nowUtc)
    {
        if (!Matches(rawToken))
        {
            throw new OnboardingRuleException("The onboarding link is not valid.");
        }

        if (Status is OnboardingInvitationStatus.Revoked)
        {
            throw new OnboardingRuleException("This onboarding invitation has been revoked.");
        }

        if (Status is OnboardingInvitationStatus.Expired || IsExpired(nowUtc))
        {
            throw new OnboardingRuleException("This onboarding link has expired.");
        }

        if (Status is OnboardingInvitationStatus.Completed)
        {
            throw new OnboardingRuleException("This onboarding invitation is already completed.");
        }

        var from = Status;
        ApplicationId = applicationId;
        if (Status == OnboardingInvitationStatus.Sent)
        {
            Status = OnboardingInvitationStatus.Opened;
            OpenedUtc = nowUtc;
        }

        Touch(nowUtc);
        return new InvitationTransition(from, Status, "Onboarding link opened", null);
    }

    /// <summary>Buyer revokes the invite. The token no longer resolves. Sent/Opened -&gt; Revoked.</summary>
    public InvitationTransition Revoke(DateTime nowUtc)
    {
        RequireLive("revoke");
        var from = Status;
        Status = OnboardingInvitationStatus.Revoked;
        RevokedUtc = nowUtc;
        Touch(nowUtc);
        return new InvitationTransition(from, Status, "Onboarding invitation revoked", null);
    }

    /// <summary>Link lapses before use. Sent/Opened -&gt; Expired.</summary>
    public InvitationTransition Expire(DateTime nowUtc)
    {
        RequireLive("expire");
        var from = Status;
        Status = OnboardingInvitationStatus.Expired;
        Touch(nowUtc);
        return new InvitationTransition(from, Status, "Onboarding link expired", null);
    }

    /// <summary>Marks the invite fulfilled once its application is approved. -&gt; Completed.</summary>
    public InvitationTransition Complete()
    {
        if (Status is OnboardingInvitationStatus.Revoked or OnboardingInvitationStatus.Expired)
        {
            throw new OnboardingRuleException($"A {Status} invitation cannot be completed.");
        }

        var from = Status;
        Status = OnboardingInvitationStatus.Completed;
        Touch();
        return new InvitationTransition(from, Status, "Onboarding invitation completed", null);
    }

    private void RequireLive(string action)
    {
        if (Status is not (OnboardingInvitationStatus.Sent or OnboardingInvitationStatus.Opened))
        {
            throw new OnboardingRuleException($"Cannot {action} a {Status} onboarding invitation.");
        }
    }
}

/// <summary>The recorded result of an invitation transition.</summary>
public sealed record InvitationTransition(
    OnboardingInvitationStatus From, OnboardingInvitationStatus To, string Action, string? Reason);

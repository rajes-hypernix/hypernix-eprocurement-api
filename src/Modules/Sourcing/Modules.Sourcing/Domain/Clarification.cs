using FSH.Framework.Core.Domain;

namespace FSH.Modules.Sourcing.Domain;

/// <summary>
/// One Q&amp;A message in an RFQ-scoped (or "general") clarification thread, keyed by
/// (Scope, VendorId) — one thread per vendor per scope. <see cref="Published"/> buyer messages
/// were fanned out to every live invited vendor's thread at send time (see
/// <c>Rfq.LiveInvitedVendorIds</c>); this row is that vendor's copy.
/// </summary>
public sealed class Clarification : AggregateRoot<Guid>
{
    /// <summary>"general" or an RFQ <c>Code</c> — never an Id, so it reads naturally in a URL/UI.</summary>
    public string Scope { get; private set; } = default!;

    /// <summary>Bare reference into Modules.Suppliers — no cross-schema FK, matching RfqInvitation.VendorId.</summary>
    public Guid VendorId { get; private set; }

    public ClarificationSenderKind SenderKind { get; private set; }
    public string SenderName { get; private set; } = default!;

    /// <summary>Set only for a buyer's targeted (non-published) reply — the specific FSH Identity user id.</summary>
    public string? RecipientUserId { get; private set; }

    public string Body { get; private set; } = default!;

    /// <summary>True for a buyer message fanned out to every live invited vendor; false for a targeted reply or a vendor's own message.</summary>
    public bool Published { get; private set; }

    public DateTime CreatedUtc { get; private set; }
    public bool ReadByBuyer { get; private set; }
    public bool ReadByVendor { get; private set; }

    private Clarification() { }

    public static Clarification Create(
        string scope,
        Guid vendorId,
        ClarificationSenderKind senderKind,
        string senderName,
        string? recipientUserId,
        string body,
        bool published,
        DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(senderName);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        return new Clarification
        {
            Id = Guid.CreateVersion7(),
            Scope = scope.Trim(),
            VendorId = vendorId,
            SenderKind = senderKind,
            SenderName = senderName.Trim(),
            RecipientUserId = recipientUserId,
            Body = body.Trim(),
            Published = published,
            CreatedUtc = nowUtc,
            // The sender has obviously "read" their own message.
            ReadByBuyer = senderKind == ClarificationSenderKind.Buyer,
            ReadByVendor = senderKind == ClarificationSenderKind.Vendor,
        };
    }

    public void MarkReadByBuyer() => ReadByBuyer = true;

    public void MarkReadByVendor() => ReadByVendor = true;
}

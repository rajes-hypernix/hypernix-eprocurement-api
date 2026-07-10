namespace eProcure.Domain.Sourcing;

/// <summary>
/// An append-only RFQ business-fact (RFQ-LIFECYCLE-ADDENDUM §2.2). Written by application services in
/// the SAME transaction as the state change it records; no update or delete path exists. Complements
/// the generic <c>AuditEntry</c> — this is the typed, queryable log powering analytics (extension
/// frequency, decline rates, days-extended). Created only via <see cref="Create"/> so every mandatory
/// field is supplied and the row is immutable thereafter (all setters private).
/// </summary>
public class RfqEvent
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid RfqId { get; private set; }
    public RfqEventType EventType { get; private set; }
    public Guid? VendorId { get; private set; }
    // Actor ids are the app's string user/vendor-login codes (e.g. "u_faridah", "VU-sentausa"),
    // matching ICurrentUser.UserId and AuditEntry.ActorId — NOT Guids. (Addendum §2.2 wrote Guid;
    // the codebase identity is string codes, so we follow the codebase. Exactly one is set.)
    public string? ActorUserId { get; private set; }
    public string? ActorVendorUserId { get; private set; }
    public string? ReasonCode { get; private set; }
    public string? ReasonNote { get; private set; }
    public DateTime? OldClosesUtc { get; private set; }   // Extended only
    public DateTime? NewClosesUtc { get; private set; }   // Extended only
    public DateTime OccurredUtc { get; private set; }

    private RfqEvent() { }   // EF

    public static RfqEvent Create(
        Guid rfqId,
        RfqEventType eventType,
        DateTime occurredUtc,
        Guid? vendorId = null,
        string? actorUserId = null,
        string? actorVendorUserId = null,
        string? reasonCode = null,
        string? reasonNote = null,
        DateTime? oldClosesUtc = null,
        DateTime? newClosesUtc = null)
    {
        if (rfqId == Guid.Empty) throw new DomainRuleException("An RFQ event requires an RFQ id.");
        return new RfqEvent
        {
            RfqId = rfqId,
            EventType = eventType,
            OccurredUtc = occurredUtc,
            VendorId = vendorId,
            ActorUserId = actorUserId,
            ActorVendorUserId = actorVendorUserId,
            ReasonCode = reasonCode,
            ReasonNote = reasonNote,
            OldClosesUtc = oldClosesUtc,
            NewClosesUtc = newClosesUtc,
        };
    }
}

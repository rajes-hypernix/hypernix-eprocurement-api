namespace FSH.Modules.Sourcing.Domain;

/// <summary>
/// An immutable, append-only business-fact row for an RFQ — separate from the generic audit
/// trail, purpose-built for RFQ-throughput analytics (release/extend/invite/rescind/close/cancel
/// timeline). Created only via <see cref="Create"/>; every property is set once.
/// </summary>
public sealed class RfqEvent
{
    public Guid Id { get; private set; }
    public Guid RfqId { get; private set; }
    public RfqEventType EventType { get; private set; }
    public Guid? VendorId { get; private set; }
    public string? ActorUserId { get; private set; }
    public string? ActorVendorUserId { get; private set; }
    public string? ReasonCode { get; private set; }
    public string? ReasonNote { get; private set; }
    public DateTime? OldClosesUtc { get; private set; }
    public DateTime? NewClosesUtc { get; private set; }
    public DateTime OccurredUtc { get; private set; }

    private RfqEvent() { }

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
        DateTime? newClosesUtc = null) => new()
        {
            Id = Guid.CreateVersion7(),
            RfqId = rfqId,
            EventType = eventType,
            VendorId = vendorId,
            ActorUserId = actorUserId,
            ActorVendorUserId = actorVendorUserId,
            ReasonCode = reasonCode,
            ReasonNote = reasonNote,
            OldClosesUtc = oldClosesUtc,
            NewClosesUtc = newClosesUtc,
            OccurredUtc = occurredUtc,
        };
}

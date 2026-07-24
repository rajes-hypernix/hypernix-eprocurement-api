using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

/// <summary>
/// Tags one record (or one of its lines) with an <see cref="OrgUnit"/> value along a given
/// dimension. A record can carry multiple tags — one per <see cref="OrgUnitType"/> dimension it's
/// assigned — but at most one per dimension per record (enforced by a unique index on
/// (RecordType, RecordId, LineId, Dimension); <see cref="Dimension"/> is denormalized from the
/// referenced <see cref="OrgUnit"/> at assignment time purely so that index doesn't need a join).
/// The controlled hierarchical value list itself is <see cref="OrgUnit"/>/<see cref="OrgUnitType"/>
/// — this entity is only the per-record assignment layer on top of it.
/// </summary>
public sealed class SegmentAssignment : AggregateRoot<Guid>, IAuditableEntity
{
    public PlatformRecordType RecordType { get; private set; }
    public Guid RecordId { get; private set; }
    public Guid? LineId { get; private set; }
    public OrgUnitType Dimension { get; private set; }
    public Guid OrgUnitId { get; private set; }

    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOnUtc { get; private set; }
    public string? LastModifiedBy { get; private set; }

    private SegmentAssignment() { }

    public static SegmentAssignment Create(
        PlatformRecordType recordType,
        Guid recordId,
        Guid? lineId,
        OrgUnitType dimension,
        Guid orgUnitId,
        string? createdBy = null)
    {
        if (recordId == Guid.Empty || orgUnitId == Guid.Empty)
        {
            throw new PlatformRuleException("A segment assignment requires both a record and an org unit.");
        }

        return new SegmentAssignment
        {
            Id = Guid.CreateVersion7(),
            RecordType = recordType,
            RecordId = recordId,
            LineId = lineId,
            Dimension = dimension,
            OrgUnitId = orgUnitId,
            CreatedOnUtc = TimeProvider.System.GetUtcNow(),
            CreatedBy = createdBy,
        };
    }

    public void Reassign(Guid orgUnitId, string? modifiedBy = null)
    {
        if (orgUnitId == Guid.Empty)
        {
            throw new PlatformRuleException("A segment assignment requires an org unit.");
        }

        OrgUnitId = orgUnitId;
        LastModifiedOnUtc = TimeProvider.System.GetUtcNow();
        LastModifiedBy = modifiedBy;
    }
}

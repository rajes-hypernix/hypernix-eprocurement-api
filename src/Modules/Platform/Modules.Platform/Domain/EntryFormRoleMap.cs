using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

/// <summary>
/// Resolves which <see cref="EntryFormDef"/> a role sees for a record type: one row per
/// (RecordType, Role). Resolution is a simple 2-level fallback — a role-specific mapping if one
/// exists, else the record type's <c>IsSystem</c> default form — deliberately simpler than the old
/// source's fixed global role-precedence chain.
/// </summary>
public sealed class EntryFormRoleMap : AggregateRoot<Guid>
{
    public PlatformRecordType RecordType { get; private set; }
    public string Role { get; private set; } = default!;
    public Guid EntryFormDefId { get; private set; }

    private EntryFormRoleMap() { }

    public static EntryFormRoleMap Create(PlatformRecordType recordType, string role, Guid entryFormDefId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        return new EntryFormRoleMap
        {
            Id = Guid.CreateVersion7(),
            RecordType = recordType,
            Role = role.Trim(),
            EntryFormDefId = entryFormDefId,
        };
    }

    public void Repoint(Guid entryFormDefId) => EntryFormDefId = entryFormDefId;
}

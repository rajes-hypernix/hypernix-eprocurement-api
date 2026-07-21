using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

public sealed class OrgUnit : AggregateRoot<Guid>
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public OrgUnitType Type { get; private set; }
    public Guid? ParentId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }

    private OrgUnit() { }

    public static OrgUnit Create(string code, string name, OrgUnitType type, Guid? parentId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var now = DateTime.UtcNow;
        return new OrgUnit
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Type = type,
            ParentId = parentId,
            CreatedUtc = now,
            UpdatedUtc = now,
        };
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedUtc = DateTime.UtcNow;
    }
}

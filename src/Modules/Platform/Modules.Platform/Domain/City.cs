using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

public sealed class City : AggregateRoot<Guid>, IAuditableEntity, ISoftDeletable
{
    public Guid StateId { get; private set; }
    public string Name { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOnUtc { get; private set; }
    public string? LastModifiedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOnUtc { get; private set; }
    public string? DeletedBy { get; private set; }

    private City() { }

    public static City Create(Guid stateId, string name, string? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (stateId == Guid.Empty)
            throw new ArgumentException("StateId is required.", nameof(stateId));

        return new City
        {
            Id = Guid.CreateVersion7(),
            StateId = stateId,
            Name = name.Trim(),
            CreatedOnUtc = TimeProvider.System.GetUtcNow(),
            CreatedBy = createdBy,
        };
    }

    public void SetActive(bool isActive, string? modifiedBy = null)
    {
        IsActive = isActive;
        LastModifiedOnUtc = TimeProvider.System.GetUtcNow();
        LastModifiedBy = modifiedBy;
    }

    public void Update(string name, string? modifiedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        LastModifiedOnUtc = TimeProvider.System.GetUtcNow();
        LastModifiedBy = modifiedBy;
    }

    public void Delete(string? deletedBy = null)
    {
        IsDeleted = true;
        DeletedOnUtc = TimeProvider.System.GetUtcNow();
        DeletedBy = deletedBy;
    }
}

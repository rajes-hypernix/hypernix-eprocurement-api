using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

public sealed class State : AggregateRoot<Guid>, IAuditableEntity, ISoftDeletable
{
    public Guid CountryId { get; private set; }
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOnUtc { get; private set; }
    public string? LastModifiedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOnUtc { get; private set; }
    public string? DeletedBy { get; private set; }

    private State() { }

    public static State Create(Guid countryId, string code, string name, string? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (countryId == Guid.Empty)
            throw new ArgumentException("CountryId is required.", nameof(countryId));

        return new State
        {
            Id = Guid.CreateVersion7(),
            CountryId = countryId,
            Code = code.Trim().ToUpperInvariant(),
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

    public void Update(string code, string name, string? modifiedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Code = code.Trim().ToUpperInvariant();
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

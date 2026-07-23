using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

public sealed class Country : AggregateRoot<Guid>, IAuditableEntity, ISoftDeletable
{
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

    private Country() { }

    public static Country Create(string code, string name, string? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = code.Trim().ToUpperInvariant();
        if (normalized.Length != 2 || !normalized.All(char.IsAsciiLetter))
            throw new PlatformRuleException("Country code must be exactly 2 letters (ISO 3166-1 alpha-2), e.g. MY.");
        return new Country
        {
            Id = Guid.CreateVersion7(),
            Code = normalized,
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
        var normalized = code.Trim().ToUpperInvariant();
        if (normalized.Length != 2 || !normalized.All(char.IsAsciiLetter))
            throw new PlatformRuleException("Country code must be exactly 2 letters (ISO 3166-1 alpha-2), e.g. MY.");
        Code = normalized;
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

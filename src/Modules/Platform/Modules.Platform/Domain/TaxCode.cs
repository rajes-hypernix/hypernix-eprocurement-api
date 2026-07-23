using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

public sealed class TaxCode : AggregateRoot<Guid>, IAuditableEntity
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    /// <summary>Percent rate (8.0 = 8%), not a fraction.</summary>
    public decimal RatePct { get; private set; }
    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOnUtc { get; private set; }
    public string? LastModifiedBy { get; private set; }

    private TaxCode() { }

    public static TaxCode Create(string code, string name, decimal ratePct, bool isActive = true, string? createdBy = null)
    {
        var t = new TaxCode
        {
            Id = Guid.CreateVersion7(),
            CreatedOnUtc = TimeProvider.System.GetUtcNow(),
            CreatedBy = createdBy,
        };
        t.Apply(code, name, ratePct, isActive, isCreate: true);
        return t;
    }

    public void Update(string name, decimal ratePct, string? modifiedBy = null)
    {
        Apply(Code, name, ratePct, IsActive, isCreate: false);
        Touch(modifiedBy);
    }

    public void SetActive(bool isActive, string? modifiedBy = null)
    {
        IsActive = isActive;
        Touch(modifiedBy);
    }

    private void Apply(string code, string name, decimal ratePct, bool isActive, bool isCreate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (ratePct is < 0 or > 100)
            throw new PlatformRuleException("Tax rate percent must be between 0 and 100.");

        if (isCreate)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(code);
            var normalized = code.Trim().ToUpperInvariant();
            if (normalized.Length > 10)
                throw new PlatformRuleException("Tax code must be at most 10 characters.");
            Code = normalized;
        }

        Name = name.Trim();
        if (Name.Length > 60)
            throw new PlatformRuleException("Tax code name must be at most 60 characters.");
        RatePct = ratePct;
        IsActive = isActive;
    }

    private void Touch(string? modifiedBy)
    {
        LastModifiedOnUtc = TimeProvider.System.GetUtcNow();
        LastModifiedBy = modifiedBy;
    }
}

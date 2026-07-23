using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

public sealed class Bank : AggregateRoot<Guid>, IAuditableEntity, ISoftDeletable
{
    public string Name { get; private set; } = default!;
    public string? SwiftCode { get; private set; }
    public string CountryCode { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOnUtc { get; private set; }
    public string? LastModifiedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOnUtc { get; private set; }
    public string? DeletedBy { get; private set; }

    private Bank() { }

    public static Bank Create(string name, string countryCode, string? swiftCode = null, string? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalizedCountry = NormalizeCountryCode(countryCode);
        return new Bank
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            CountryCode = normalizedCountry,
            SwiftCode = NormalizeSwift(swiftCode),
            CreatedOnUtc = TimeProvider.System.GetUtcNow(),
            CreatedBy = createdBy,
        };
    }

    public void Update(string name, string countryCode, string? swiftCode = null, string? modifiedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        CountryCode = NormalizeCountryCode(countryCode);
        SwiftCode = NormalizeSwift(swiftCode);
        LastModifiedOnUtc = TimeProvider.System.GetUtcNow();
        LastModifiedBy = modifiedBy;
    }

    public void SetActive(bool isActive, string? modifiedBy = null)
    {
        IsActive = isActive;
        LastModifiedOnUtc = TimeProvider.System.GetUtcNow();
        LastModifiedBy = modifiedBy;
    }

    public void Delete(string? deletedBy = null)
    {
        IsDeleted = true;
        DeletedOnUtc = TimeProvider.System.GetUtcNow();
        DeletedBy = deletedBy;
    }

    private static string NormalizeCountryCode(string countryCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        var code = countryCode.Trim().ToUpperInvariant();
        if (code.Length != 2 || !code.All(char.IsAsciiLetter))
            throw new PlatformRuleException("Country code must be exactly 2 letters (ISO 3166-1 alpha-2), e.g. MY.");
        return code;
    }

    private static string? NormalizeSwift(string? swiftCode)
    {
        if (string.IsNullOrWhiteSpace(swiftCode)) return null;
        var swift = swiftCode.Trim().ToUpperInvariant();
        if (swift.Length > 20)
            throw new PlatformRuleException("SWIFT/BIC must be at most 20 characters.");
        return swift;
    }
}

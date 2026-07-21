using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

public sealed class Bank : AggregateRoot<Guid>
{
    public string Name { get; private set; } = default!;
    public string? SwiftCode { get; private set; }
    public string CountryCode { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }

    private Bank() { }

    public static Bank Create(string name, string countryCode, string? swiftCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        var now = DateTime.UtcNow;
        return new Bank
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            CountryCode = countryCode.Trim().ToUpperInvariant(),
            SwiftCode = string.IsNullOrWhiteSpace(swiftCode) ? null : swiftCode.Trim().ToUpperInvariant(),
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

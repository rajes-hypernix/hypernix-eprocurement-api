using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

public sealed class Currency : AggregateRoot<Guid>, IAuditableEntity
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string Symbol { get; private set; } = default!;
    public int Decimals { get; private set; } = 2;
    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOnUtc { get; private set; }
    public string? LastModifiedBy { get; private set; }

    private Currency() { }

    public static Currency Create(
        string code,
        string name,
        string symbol,
        int decimals = 2,
        bool isActive = true,
        string? createdBy = null)
    {
        var c = new Currency
        {
            Id = Guid.CreateVersion7(),
            CreatedOnUtc = TimeProvider.System.GetUtcNow(),
            CreatedBy = createdBy,
        };
        c.Apply(code, name, symbol, decimals, isActive, createdBy, isCreate: true);
        return c;
    }

    public void Update(string name, string symbol, int decimals, string? modifiedBy = null)
        => Apply(Code, name, symbol, decimals, IsActive, modifiedBy, isCreate: false);

    public void SetActive(bool isActive, string? modifiedBy = null)
    {
        IsActive = isActive;
        Touch(modifiedBy);
    }

    private void Apply(string code, string name, string symbol, int decimals, bool isActive, string? by, bool isCreate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (decimals is < 0 or > 4)
            throw new PlatformRuleException("Currency decimals must be between 0 and 4.");

        if (isCreate)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(code);
            var normalized = code.Trim().ToUpperInvariant();
            if (normalized.Length is < 1 or > 3 || !normalized.All(char.IsAsciiLetter))
                throw new PlatformRuleException("Currency code must be 1–3 letters (ISO 4217), e.g. MYR.");
            Code = normalized;
        }

        Name = name.Trim();
        Symbol = symbol.Trim();
        if (Symbol.Length > 6)
            throw new PlatformRuleException("Currency symbol must be at most 6 characters.");
        Decimals = decimals;
        IsActive = isActive;
        if (!isCreate) Touch(by);
    }

    private void Touch(string? modifiedBy)
    {
        LastModifiedOnUtc = TimeProvider.System.GetUtcNow();
        LastModifiedBy = modifiedBy;
    }
}

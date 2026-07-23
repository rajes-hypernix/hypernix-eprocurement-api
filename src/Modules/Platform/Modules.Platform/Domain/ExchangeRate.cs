using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

/// <summary>Append-only FX ledger. RateToBase = units of base currency per 1 unit of foreign.</summary>
public sealed class ExchangeRate : AggregateRoot<Guid>, IAuditableEntity
{
    public string CurrencyCode { get; private set; } = default!;
    public decimal RateToBase { get; private set; }
    public DateOnly EffectiveDate { get; private set; }
    public string EnteredByUserId { get; private set; } = default!;

    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CreatedBy { get; private set; }

    /// <summary>Always null: exchange rates are append-only and never modified after creation.</summary>
    public DateTimeOffset? LastModifiedOnUtc { get; }

    /// <summary>Always null: exchange rates are append-only and never modified after creation.</summary>
    public string? LastModifiedBy { get; }

    private ExchangeRate() { }

    public static ExchangeRate Create(
        string currencyCode,
        decimal rateToBase,
        DateOnly effectiveDate,
        string enteredByUserId,
        string? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(enteredByUserId);
        if (rateToBase <= 0)
            throw new PlatformRuleException("Exchange rate must be greater than zero.");

        var code = currencyCode.Trim().ToUpperInvariant();
        return new ExchangeRate
        {
            Id = Guid.CreateVersion7(),
            CurrencyCode = code,
            RateToBase = rateToBase,
            EffectiveDate = effectiveDate,
            EnteredByUserId = enteredByUserId.Trim(),
            CreatedOnUtc = TimeProvider.System.GetUtcNow(),
            CreatedBy = createdBy ?? enteredByUserId.Trim(),
        };
    }
}

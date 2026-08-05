namespace FSH.Modules.Suppliers.Domain;

public sealed class VendorBankAccount
{
    /// <summary>Logical FK to Platform <c>Banks.Id</c>.</summary>
    public Guid BankId { get; private set; }
    /// <summary>Denormalized bank name snapshot for display without cross-module joins.</summary>
    public string BankName { get; private set; }
    public string AccountNo { get; private set; }
    public string Swift { get; private set; }
    /// <summary>ISO 4217 — logical FK to Platform <c>Currencies.Code</c>.</summary>
    public string CurrencyCode { get; private set; }
    public bool IsPrimary { get; private set; }

    public VendorBankAccount(
        Guid bankId,
        string bankName,
        string? accountNo,
        string? swift,
        string? currencyCode,
        bool isPrimary)
    {
        if (bankId == Guid.Empty)
            throw new ArgumentException("BankId is required.", nameof(bankId));
        ArgumentException.ThrowIfNullOrWhiteSpace(bankName);

        BankId = bankId;
        BankName = bankName.Trim();
        AccountNo = (accountNo ?? string.Empty).Trim();
        Swift = (swift ?? string.Empty).Trim().ToUpperInvariant();
        CurrencyCode = NormalizeCurrency(currencyCode);
        IsPrimary = isPrimary;
    }

    private static string NormalizeCurrency(string? currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode)) return "MYR";
        var code = currencyCode.Trim().ToUpperInvariant();
        if (code.Length != 3 || !code.All(char.IsAsciiLetter))
            throw new ArgumentException("Currency code must be ISO 4217 (e.g. MYR).");
        return code;
    }
}

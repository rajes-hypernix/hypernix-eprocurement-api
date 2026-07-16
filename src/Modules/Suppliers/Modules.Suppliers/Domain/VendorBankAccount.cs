namespace FSH.Modules.Suppliers.Domain;

public sealed class VendorBankAccount
{
    public string Bank { get; private set; }
    public string AccountNo { get; private set; }
    public string Swift { get; private set; }
    public string Currency { get; private set; }
    public bool IsPrimary { get; private set; }

    public VendorBankAccount(string bank, string? accountNo, string? swift, string? currency, bool isPrimary)
    {
        Bank = bank;
        AccountNo = accountNo ?? string.Empty;
        Swift = swift ?? string.Empty;
        Currency = string.IsNullOrWhiteSpace(currency) ? "MYR" : currency;
        IsPrimary = isPrimary;
    }
}

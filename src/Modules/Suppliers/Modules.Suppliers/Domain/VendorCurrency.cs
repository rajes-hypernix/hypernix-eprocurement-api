namespace FSH.Modules.Suppliers.Domain;

public sealed class VendorCurrency
{
    public string Code { get; private set; }
    public bool IsPrimary { get; private set; }

    public VendorCurrency(string? code, bool isPrimary)
    {
        Code = string.IsNullOrWhiteSpace(code) ? "MYR" : code;
        IsPrimary = isPrimary;
    }
}

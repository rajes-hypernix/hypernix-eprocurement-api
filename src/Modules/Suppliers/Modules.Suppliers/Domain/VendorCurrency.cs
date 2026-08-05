namespace FSH.Modules.Suppliers.Domain;

public sealed class VendorCurrency
{
    public string Code { get; private set; }
    public bool IsPrimary { get; private set; }

    public VendorCurrency(string? code, bool isPrimary)
    {
        Code = Normalize(code);
        IsPrimary = isPrimary;
    }

    private static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "MYR";
        var c = code.Trim().ToUpperInvariant();
        if (c.Length != 3 || !c.All(char.IsAsciiLetter))
            throw new ArgumentException("Currency code must be ISO 4217 (e.g. MYR).");
        return c;
    }
}

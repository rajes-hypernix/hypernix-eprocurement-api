namespace FSH.Modules.Suppliers.Domain;

public sealed class VendorCertification
{
    public string Name { get; private set; }
    public string Number { get; private set; }
    public string ValidTo { get; private set; }
    public string Status { get; private set; }

    public VendorCertification(string name, string? number, string? validTo, string? status)
    {
        Name = name;
        Number = number ?? string.Empty;
        ValidTo = validTo ?? string.Empty;
        Status = string.IsNullOrWhiteSpace(status) ? "Valid" : status;
    }
}

namespace FSH.Modules.Suppliers.Domain;

public sealed class VendorCertification
{
    public string Name { get; private set; }
    public string Number { get; private set; }
    public string ValidTo { get; private set; }
    public CertificationStatus Status { get; private set; }

    public VendorCertification(string name, string? number, string? validTo, CertificationStatus status = CertificationStatus.Valid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Number = (number ?? string.Empty).Trim();
        ValidTo = (validTo ?? string.Empty).Trim();
        Status = status;
    }
}

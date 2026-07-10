namespace eProcure.Domain.Suppliers;

/// <summary>
/// A supplier-portal login, a SEPARATE principal type from internal users, linked
/// to exactly one Vendor. Always holds the Vendor role and may only read/write its
/// own vendor's records (access scoping, BUSINESS-RULES [G]).
/// </summary>
public class VendorUser
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; private set; } = default!;
    public Guid VendorId { get; private set; }
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    private VendorUser() { }

    public VendorUser(string code, Guid vendorId, string name, string email)
    {
        Id = Guid.NewGuid();
        Code = code;
        VendorId = vendorId;
        Name = name;
        Email = email;
    }
}

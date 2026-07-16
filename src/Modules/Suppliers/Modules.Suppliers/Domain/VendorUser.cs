using FSH.Framework.Core.Domain;

namespace FSH.Modules.Suppliers.Domain;

/// <summary>
/// A vendor-portal login, distinct from internal FSH Identity users. Only created via
/// onboarding approval — there is no standalone VendorUser CRUD in the old system either.
/// </summary>
public sealed class VendorUser : AggregateRoot<Guid>
{
    public string Code { get; private set; } = default!;
    public Guid VendorId { get; private set; }
    public string Name { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }

    private VendorUser() { }

    public static VendorUser Create(string code, Guid vendorId, string name, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var now = DateTime.UtcNow;
        return new VendorUser
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim(),
            VendorId = vendorId,
            Name = name.Trim(),
            Email = email.Trim(),
            CreatedUtc = now,
            UpdatedUtc = now,
        };
    }
}

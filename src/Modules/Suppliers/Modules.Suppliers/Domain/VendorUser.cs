using FSH.Framework.Core.Domain;

namespace FSH.Modules.Suppliers.Domain;

/// <summary>
/// A vendor-portal login, distinct from internal FSH Identity users. Only created via
/// onboarding approval — there is no standalone VendorUser CRUD in the old system either.
/// </summary>
public sealed class VendorUser : AggregateRoot<Guid>, IAuditableEntity
{
    public string Code { get; private set; } = default!;
    public Guid VendorId { get; private set; }
    public string Name { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOnUtc { get; private set; }
    public string? LastModifiedBy { get; private set; }

    /// <summary>The provisioned FSH Identity user's Id (string — matches AspNetUsers.Id), once vendor login is set up.</summary>
    public string? IdentityUserId { get; private set; }

    private VendorUser() { }

    public static VendorUser Create(string code, Guid vendorId, string name, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        return new VendorUser
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim(),
            VendorId = vendorId,
            Name = name.Trim(),
            Email = email.Trim(),
            CreatedOnUtc = AuditTime.UtcNow,
            CreatedBy = null,
        };
    }

    /// <summary>Links this vendor-portal login to a provisioned FSH Identity user, once one exists.</summary>
    public void LinkIdentity(string identityUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityUserId);
        IdentityUserId = identityUserId;
        LastModifiedOnUtc = AuditTime.UtcNow;
        LastModifiedBy = null;
    }
}

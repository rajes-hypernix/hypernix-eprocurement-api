namespace eProcure.Domain.Identity;

/// <summary>
/// An internal Hypernix/SPSB user. Internal users are NEVER vendors (SoD): the
/// <see cref="Vendor"/> role is rejected by <see cref="SetRoles"/>.
/// </summary>
public class User
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; private set; } = default!;
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public List<string> Roles { get; private set; } = [];
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    private User() { }

    public User(string code, string name, string email, IEnumerable<string> roles)
    {
        Id = Guid.NewGuid();
        Code = code;
        Name = name;
        Email = email;
        SetRoles(roles);
    }

    /// <summary>Replaces the role set, rejecting the vendor role (SoD guard).</summary>
    public void SetRoles(IEnumerable<string> roles)
    {
        var distinct = roles.Distinct().ToList();
        var invalid = distinct.Where(r => !Identity.Roles.IsInternal(r)).ToList();
        if (invalid.Count > 0)
            throw new DomainRuleException(
                $"Internal users cannot hold the role(s): {string.Join(", ", invalid)}. " +
                "Internal users are never vendors (segregation of duties).");
        Roles = distinct;
    }
}

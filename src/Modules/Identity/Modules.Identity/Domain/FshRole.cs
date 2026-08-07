using Microsoft.AspNetCore.Identity;

namespace FSH.Modules.Identity.Domain;

public class FshRole : IdentityRole
{
    public string? Description { get; set; }

    /// <summary>When the role was created (UTC).</summary>
    public DateTimeOffset CreatedOnUtc { get; set; }

    public FshRole(string name, string? description = null)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(name);

        Description = description;
        NormalizedName = name.ToUpperInvariant();
        CreatedOnUtc = TimeProvider.System.GetUtcNow();
    }
}
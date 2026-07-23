using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

public sealed class Incoterm : AggregateRoot<Guid>, IAuditableEntity
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOnUtc { get; private set; }
    public string? LastModifiedBy { get; private set; }

    private Incoterm() { }

    public static Incoterm Create(string code, string name, bool isActive = true, string? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Incoterm
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            IsActive = isActive,
            CreatedOnUtc = TimeProvider.System.GetUtcNow(),
            CreatedBy = createdBy,
        };
    }

    public void Update(string name, string? modifiedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        LastModifiedOnUtc = TimeProvider.System.GetUtcNow();
        LastModifiedBy = modifiedBy;
    }

    public void SetActive(bool isActive, string? modifiedBy = null)
    {
        IsActive = isActive;
        LastModifiedOnUtc = TimeProvider.System.GetUtcNow();
        LastModifiedBy = modifiedBy;
    }

    public static string Compose(string? code, string? suffix)
    {
        var c = string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
        var s = string.IsNullOrWhiteSpace(suffix) ? null : suffix.Trim();
        if (c is null) return s ?? string.Empty;
        return s is null ? c : $"{c} {s}";
    }

    public static (string? Code, string? Suffix) Parse(string? incoterm, IReadOnlySet<string> knownCodes)
    {
        ArgumentNullException.ThrowIfNull(knownCodes);
        if (string.IsNullOrWhiteSpace(incoterm)) return (null, null);
        var parts = incoterm.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var first = parts[0].ToUpperInvariant();
        if (knownCodes.Contains(first))
            return (first, parts.Length > 1 ? parts[1].Trim() : null);
        return (null, incoterm.Trim());
    }
}

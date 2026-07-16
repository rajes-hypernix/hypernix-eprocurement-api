namespace FSH.Modules.Suppliers.Domain;

/// <summary>
/// PETRONAS-style SWEC taxonomy (Discipline -&gt; Group -&gt; Item), flattened with parent-code links.
/// Reference data, not a Guid-keyed aggregate — <see cref="Code"/> is the natural key.
/// </summary>
public sealed class SwecCategory
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? ParentCode { get; private set; }
    public int Level { get; private set; }
    public bool IsLeaf { get; private set; }
    public string PathText { get; private set; } = default!;

    private SwecCategory() { }

    public static SwecCategory Create(string code, string name, string? parentCode, int level, bool isLeaf, string pathText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(pathText);

        return new SwecCategory
        {
            Code = code,
            Name = name,
            ParentCode = parentCode,
            Level = level,
            IsLeaf = isLeaf,
            PathText = pathText,
        };
    }
}

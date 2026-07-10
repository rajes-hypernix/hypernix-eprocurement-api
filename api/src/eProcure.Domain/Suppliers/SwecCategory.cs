namespace eProcure.Domain.Suppliers;

/// <summary>
/// A node in the PETRONAS-style SWEC taxonomy (Discipline → Group → Class → Item).
/// Vendors tag one or more codes at any tier. Reference/seed data.
/// </summary>
public class SwecCategory
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? ParentCode { get; private set; }
    public int Level { get; private set; }
    public bool IsLeaf { get; private set; }

    /// <summary>Full breadcrumb, e.g. "Mechanical › Rotating equipment › Centrifugal pumps".</summary>
    public string PathText { get; private set; } = default!;

    private SwecCategory() { }

    public SwecCategory(string code, string name, string? parentCode, int level, bool isLeaf, string pathText)
    {
        Code = code;
        Name = name;
        ParentCode = parentCode;
        Level = level;
        IsLeaf = isLeaf;
        PathText = pathText;
    }
}

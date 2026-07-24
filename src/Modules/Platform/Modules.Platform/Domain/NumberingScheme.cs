using FSH.Framework.Core.Domain;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FSH.Modules.Platform.Domain;

/// <summary>Document types that can own a numbering scheme.</summary>
public static class DocumentNumberRecordTypes
{
    public const string PurchaseRequisition = "PR";
    public const string Rfq = "RFQ";
    public const string PurchaseOrder = "PO";
    public const string Invoice = "INV";
    public const string Asn = "ASN";
    public const string Grn = "GRN";
    public const string VendorOnboarding = "VOB";
    public const string SwecVendor = "SWK-V";

    public static readonly IReadOnlyList<string> All =
    [
        PurchaseRequisition, Rfq, PurchaseOrder, Invoice, Asn, Grn, VendorOnboarding, SwecVendor,
    ];
}

public sealed class NumberingScheme : AggregateRoot<Guid>, IAuditableEntity
{
    private static readonly Regex PrefixRegex = new("^[A-Z0-9][A-Z0-9-]{0,11}$", RegexOptions.Compiled);

    public string RecordType { get; private set; } = default!;
    public string Prefix { get; private set; } = default!;
    public bool YearSegment { get; private set; } = true;
    public int Digits { get; private set; } = 4;

    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOnUtc { get; private set; }
    public string? LastModifiedBy { get; private set; }

    private NumberingScheme() { }

    public static NumberingScheme Create(
        string recordType,
        string prefix,
        bool yearSegment = true,
        int digits = 4,
        Guid? id = null,
        string? createdBy = null)
    {
        var scheme = new NumberingScheme
        {
            Id = id ?? Guid.CreateVersion7(),
            CreatedOnUtc = TimeProvider.System.GetUtcNow(),
            CreatedBy = createdBy,
        };
        scheme.Apply(recordType, prefix, yearSegment, digits, isCreate: true);
        return scheme;
    }

    public void Update(string prefix, bool yearSegment, int digits, string? modifiedBy = null)
    {
        Apply(RecordType, prefix, yearSegment, digits, isCreate: false);
        LastModifiedOnUtc = TimeProvider.System.GetUtcNow();
        LastModifiedBy = modifiedBy;
    }

    public string FormatPreview(int nextValue, int year)
    {
        var padded = nextValue.ToString(CultureInfo.InvariantCulture).PadLeft(Digits, '0');
        return YearSegment ? $"{Prefix}-{year}-{padded}" : $"{Prefix}-{padded}";
    }

    private void Apply(string recordType, string prefix, bool yearSegment, int digits, bool isCreate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        if (digits is < 3 or > 6)
            throw new PlatformRuleException("Numbering digits must be between 3 and 6.");

        var p = prefix.Trim().ToUpperInvariant();
        if (!PrefixRegex.IsMatch(p))
            throw new PlatformRuleException("Prefix must match ^[A-Z0-9][A-Z0-9-]{0,11}$.");

        if (isCreate)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(recordType);
            RecordType = recordType.Trim().ToUpperInvariant();
        }

        Prefix = p;
        YearSegment = yearSegment;
        Digits = digits;
    }
}

/// <summary>Atomic counter bucket for a prefix (+ optional year). Never reset.</summary>
public sealed class NumberSequence : IAuditableEntity
{
    public string Prefix { get; private set; } = default!;
    public int Year { get; private set; }
    public int LastValue { get; private set; }

    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOnUtc { get; private set; }
    public string? LastModifiedBy { get; private set; }

    private NumberSequence() { }

    public static NumberSequence Create(string prefix, int year, string? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        return new NumberSequence
        {
            Prefix = prefix.Trim().ToUpperInvariant(),
            Year = year,
            LastValue = 0,
            CreatedOnUtc = TimeProvider.System.GetUtcNow(),
            CreatedBy = createdBy,
        };
    }

    public int Next(string? modifiedBy = null)
    {
        LastValue++;
        LastModifiedOnUtc = TimeProvider.System.GetUtcNow();
        LastModifiedBy = modifiedBy;
        return LastValue;
    }

    public void AdvanceTo(int value, string? modifiedBy = null)
    {
        if (value < LastValue)
            throw new PlatformRuleException("Cannot rewind a number sequence.");
        LastValue = value;
        LastModifiedOnUtc = TimeProvider.System.GetUtcNow();
        LastModifiedBy = modifiedBy;
    }
}

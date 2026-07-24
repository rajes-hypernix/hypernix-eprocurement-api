using FSH.Modules.Platform.Domain;

namespace FSH.Modules.Platform.Features.v1;

/// <summary>
/// Which of a record type's own hand-entered properties may be placed on an entry form, and how to
/// render them — mirrors the old source's <c>EntryFormVocabulary.ControllableNativeKeys</c>. A
/// record type not listed here (or listed with no keys) has forms that are purely custom-field/
/// segment placement containers, same as the old source's PurchaseOrder/Grn forms.
/// </summary>
internal static class EntryFormVocabulary
{
    internal sealed record NativeField(string Key, string Label, string DataType);

    private static readonly IReadOnlyDictionary<PlatformRecordType, IReadOnlyList<NativeField>> ControllableNativeKeys =
        new Dictionary<PlatformRecordType, IReadOnlyList<NativeField>>
        {
            [PlatformRecordType.Requisition] =
            [
                new("Requestor", "Requestor", "Text"),
                new("Department", "Department", "Text"),
                new("Location", "Location", "Text"),
                new("Category", "Category", "Text"),
                new("Job", "Job", "Text"),
                new("Memo", "Memo", "LongText"),
                new("RequiredOn", "Required on", "Date"),
            ],
            [PlatformRecordType.PurchaseOrder] =
            [
                new("Memo", "Memo", "LongText"),
                new("VendorRef", "Vendor reference", "Text"),
                new("RequiredDate", "Required date", "Date"),
                new("DeliveryDate", "Delivery date", "Date"),
            ],
            [PlatformRecordType.Vendor] = [],
        };

    internal static NativeField? Find(PlatformRecordType recordType, string fieldKey) =>
        ControllableNativeKeys.TryGetValue(recordType, out var fields)
            ? fields.FirstOrDefault(f => string.Equals(f.Key, fieldKey, StringComparison.OrdinalIgnoreCase))
            : null;
}

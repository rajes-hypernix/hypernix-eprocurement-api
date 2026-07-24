using System.Security.Cryptography;
using System.Text;
using FSH.Modules.Platform.Domain;

namespace FSH.Modules.Platform.Data;

/// <summary>
/// Native field registry (one row per scalar the Requisition/Rfq list screens render) plus the
/// two system "All ..." saved views. THE single source for wave 1 — later slices add rows here,
/// they never reshape the registry.
/// </summary>
public static class ViewsSeedData
{
    private sealed record FieldRow(ViewRecordType RecordType, string FieldKey, ViewFieldDataType DataType, string Label);

    /// <summary>Deterministic id per (RecordType, FieldKey) so re-seeding stays idempotent without a literal GUID
    /// table. Not security-sensitive — just a stable hash truncated to 16 bytes for a <see cref="Guid"/>.</summary>
    public static Guid StableFieldId(ViewRecordType recordType, string fieldKey) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"fieldregistry:{recordType}:{fieldKey}"))[..16]);

    private static readonly IReadOnlyList<FieldRow> NativeFields =
    [
        new(ViewRecordType.Requisition, "Code", ViewFieldDataType.Code, "PR #"),
        new(ViewRecordType.Requisition, "Requestor", ViewFieldDataType.Text, "Requestor"),
        new(ViewRecordType.Requisition, "Department", ViewFieldDataType.Text, "Department"),
        new(ViewRecordType.Requisition, "Location", ViewFieldDataType.Text, "Location"),
        new(ViewRecordType.Requisition, "Memo", ViewFieldDataType.Text, "Memo"),
        new(ViewRecordType.Requisition, "Job", ViewFieldDataType.Text, "Job"),
        new(ViewRecordType.Requisition, "Category", ViewFieldDataType.Text, "Category"),
        new(ViewRecordType.Requisition, "CostCentre", ViewFieldDataType.Text, "Cost centre"),
        new(ViewRecordType.Requisition, "Project", ViewFieldDataType.Text, "Project"),
        new(ViewRecordType.Requisition, "RaisedDate", ViewFieldDataType.Date, "Raised"),
        new(ViewRecordType.Requisition, "RequiredDate", ViewFieldDataType.Date, "Required by"),
        new(ViewRecordType.Requisition, "HeaderStatus", ViewFieldDataType.Enum, "PR status"),
        new(ViewRecordType.Requisition, "Value", ViewFieldDataType.Money, "Value"),
        new(ViewRecordType.Requisition, "Submitted", ViewFieldDataType.Bool, "Submitted"),

        new(ViewRecordType.Rfq, "Code", ViewFieldDataType.Code, "RFQ"),
        new(ViewRecordType.Rfq, "Title", ViewFieldDataType.Text, "Title"),
        new(ViewRecordType.Rfq, "Envelope", ViewFieldDataType.Enum, "Envelope"),
        new(ViewRecordType.Rfq, "Status", ViewFieldDataType.Enum, "Status"),
        new(ViewRecordType.Rfq, "Currency", ViewFieldDataType.Code, "Currency"),
        new(ViewRecordType.Rfq, "ClosesUtc", ViewFieldDataType.Instant, "Closes"),
        new(ViewRecordType.Rfq, "InvitedCount", ViewFieldDataType.Number, "Vendors"),
        new(ViewRecordType.Rfq, "LineCount", ViewFieldDataType.Number, "Lines"),
        new(ViewRecordType.Rfq, "QuestionCount", ViewFieldDataType.Number, "Questions"),
        new(ViewRecordType.Rfq, "BidCount", ViewFieldDataType.Number, "Bids"),
        // Raw identity user id, not really meant for a picklist — exists so a "My RFQs" view can
        // filter OwnerUserId Eq @me, the concrete demonstration of the Phase 7 current-user token
        // (see SavedViewFilterExecutor).
        new(ViewRecordType.Rfq, "OwnerUserId", ViewFieldDataType.Text, "Owner (internal)"),

        new(ViewRecordType.PurchaseOrder, "Code", ViewFieldDataType.Code, "PO #"),
        new(ViewRecordType.PurchaseOrder, "Status", ViewFieldDataType.Enum, "Status"),
        new(ViewRecordType.PurchaseOrder, "SourceKind", ViewFieldDataType.Enum, "Source"),
        new(ViewRecordType.PurchaseOrder, "Currency", ViewFieldDataType.Code, "Currency"),
        // One-hop related field: resolved via Suppliers' whitelist-only IVendorLookupService
        // (Name/Code only — never bank details) rather than a raw VendorId. See ProcurementSavedViewRowSource.
        new(ViewRecordType.PurchaseOrder, "VendorName", ViewFieldDataType.Text, "Vendor"),
        new(ViewRecordType.PurchaseOrder, "TotalValue", ViewFieldDataType.Money, "Value"),
        new(ViewRecordType.PurchaseOrder, "RequiredDate", ViewFieldDataType.Date, "Required by"),
        new(ViewRecordType.PurchaseOrder, "DeliveryDate", ViewFieldDataType.Date, "Delivery"),
        new(ViewRecordType.PurchaseOrder, "IssuedUtc", ViewFieldDataType.Instant, "Issued"),

        new(ViewRecordType.Vendor, "Code", ViewFieldDataType.Code, "Vendor #"),
        new(ViewRecordType.Vendor, "Name", ViewFieldDataType.Text, "Name"),
        new(ViewRecordType.Vendor, "Status", ViewFieldDataType.Enum, "Status"),
        new(ViewRecordType.Vendor, "Type", ViewFieldDataType.Enum, "Type"),
        new(ViewRecordType.Vendor, "Region", ViewFieldDataType.Text, "Region"),
        new(ViewRecordType.Vendor, "State", ViewFieldDataType.Text, "State"),
        new(ViewRecordType.Vendor, "City", ViewFieldDataType.Text, "City"),
        new(ViewRecordType.Vendor, "Rating", ViewFieldDataType.Number, "Rating"),
        new(ViewRecordType.Vendor, "CreditLimit", ViewFieldDataType.Money, "Credit limit"),
    ];

    /// <summary>
    /// Segments piggyback on the fixed <see cref="OrgUnitType"/> dimension set rather than a
    /// per-tenant definition table, so (unlike Custom fields, synced live on Apply/Remove — see
    /// <c>ApplyCustomFieldToRecordTypeCommandHandler</c>) these rows are static and safe to seed
    /// once, cross-joined over every <see cref="PlatformRecordType"/>-aligned record type.
    /// </summary>
    private static readonly IReadOnlyList<ViewRecordType> SegmentableRecordTypes =
    [
        ViewRecordType.Requisition,
        ViewRecordType.PurchaseOrder,
        ViewRecordType.Vendor,
    ];

    public static string SegmentFieldKey(OrgUnitType dimension) => $"Segment_{dimension}";

    /// <summary>
    /// Phase 7 note: the original wave-1 seed gated everything behind
    /// <c>if (!db.FieldRegistryEntries.Any())</c> / <c>if (!db.SavedViews.Any(v =&gt; v.IsSystem))</c> —
    /// all-or-nothing per table, so a dev DB that already has Requisition/Rfq rows would never pick
    /// up the new PurchaseOrder/Vendor/Segment rows added here on a re-run. Switched to a per-key
    /// idempotency check (existing ids loaded once, missing ones added) so extending the registry is
    /// truly additive against a live database, not just against a fresh one.
    /// </summary>
    public static void Seed(PlatformDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);

        var existingFieldIds = db.FieldRegistryEntries.Select(f => f.Id).ToHashSet();

        foreach (var row in NativeFields)
        {
            var id = StableFieldId(row.RecordType, row.FieldKey);
            if (existingFieldIds.Contains(id))
                continue;

            db.FieldRegistryEntries.Add(FieldRegistryEntry.Create(id, row.RecordType, row.FieldKey, row.DataType, row.Label));
        }

        foreach (var recordType in SegmentableRecordTypes)
        {
            foreach (var dimension in Enum.GetValues<OrgUnitType>())
            {
                var fieldKey = SegmentFieldKey(dimension);
                var id = StableFieldId(recordType, fieldKey);
                if (existingFieldIds.Contains(id))
                    continue;

                db.FieldRegistryEntries.Add(FieldRegistryEntry.Create(
                    id, recordType, fieldKey, ViewFieldDataType.Text, $"Segment: {dimension}", ViewFieldKind.Segment));
            }
        }

        var existingViewCodes = db.SavedViews.Select(v => v.Code).ToHashSet();

        if (!existingViewCodes.Contains("SYS-REQ-ALL"))
        {
            db.SavedViews.Add(SavedView.Create(
                "SYS-REQ-ALL",
                "All Requisitions",
                ViewRecordType.Requisition,
                ownerUserId: null,
                isShared: true,
                isSystem: true,
                filters: [],
                columns:
                [
                    new SavedViewColumnInput("Code", null, 0, ViewSortDirection.Desc),
                    new SavedViewColumnInput("Requestor", null, 1, null),
                    new SavedViewColumnInput("Department", null, 2, null),
                    new SavedViewColumnInput("HeaderStatus", null, 3, null),
                    new SavedViewColumnInput("Value", null, 4, null),
                    new SavedViewColumnInput("RaisedDate", null, 5, null),
                ]));
        }

        if (!existingViewCodes.Contains("SYS-RFQ-ALL"))
        {
            db.SavedViews.Add(SavedView.Create(
                "SYS-RFQ-ALL",
                "All RFQs",
                ViewRecordType.Rfq,
                ownerUserId: null,
                isShared: true,
                isSystem: true,
                filters: [],
                columns:
                [
                    new SavedViewColumnInput("Code", null, 0, ViewSortDirection.Desc),
                    new SavedViewColumnInput("Title", null, 1, null),
                    new SavedViewColumnInput("Status", null, 2, null),
                    new SavedViewColumnInput("Currency", null, 3, null),
                    new SavedViewColumnInput("ClosesUtc", null, 4, null),
                    new SavedViewColumnInput("InvitedCount", null, 5, null),
                ]));
        }

        if (!existingViewCodes.Contains("SYS-PO-ALL"))
        {
            db.SavedViews.Add(SavedView.Create(
                "SYS-PO-ALL",
                "All Purchase Orders",
                ViewRecordType.PurchaseOrder,
                ownerUserId: null,
                isShared: true,
                isSystem: true,
                filters: [],
                columns:
                [
                    new SavedViewColumnInput("Code", null, 0, ViewSortDirection.Desc),
                    new SavedViewColumnInput("VendorName", null, 1, null),
                    new SavedViewColumnInput("Status", null, 2, null),
                    new SavedViewColumnInput("TotalValue", null, 3, null),
                    new SavedViewColumnInput("RequiredDate", null, 4, null),
                ]));
        }

        if (!existingViewCodes.Contains("SYS-VENDOR-ALL"))
        {
            db.SavedViews.Add(SavedView.Create(
                "SYS-VENDOR-ALL",
                "All Vendors",
                ViewRecordType.Vendor,
                ownerUserId: null,
                isShared: true,
                isSystem: true,
                filters: [],
                columns:
                [
                    new SavedViewColumnInput("Code", null, 0, ViewSortDirection.Desc),
                    new SavedViewColumnInput("Name", null, 1, null),
                    new SavedViewColumnInput("Status", null, 2, null),
                    new SavedViewColumnInput("Type", null, 3, null),
                    new SavedViewColumnInput("Rating", null, 4, null),
                ]));
        }
    }
}

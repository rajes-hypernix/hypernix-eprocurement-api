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
    ];

    public static void Seed(PlatformDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);

        if (!db.FieldRegistryEntries.Any())
        {
            foreach (var row in NativeFields)
            {
                db.FieldRegistryEntries.Add(FieldRegistryEntry.Create(
                    StableFieldId(row.RecordType, row.FieldKey),
                    row.RecordType,
                    row.FieldKey,
                    row.DataType,
                    row.Label));
            }
        }

        if (!db.SavedViews.Any(v => v.IsSystem))
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
    }
}

using System.Security.Cryptography;
using System.Text;
using eProcure.Domain.Views;

namespace eProcure.Application.Views;

/// <summary>
/// The Native field registry seed — D3 Step 0(b) as ruled: one row per scalar property the
/// list DTOs render, keyed by DTO property name (DATA-MODEL naming). THE single source: the
/// SavedViews migration inserts these rows, tests seed in-memory stores from them, and the
/// reflection drift-test pins each row's DataType to its DTO property type. The legacy
/// Requisition `Status` string (duplicate of HeaderStatus, BACKLOG convergence row) is
/// deliberately NOT seeded — the registry must not enshrine a field slated for deletion.
/// </summary>
public static class FieldRegistrySeed
{
    public sealed record Row(RecordType RecordType, string FieldKey, FieldDataType DataType, string Label);

    /// <summary>Deterministic id per (RecordType, FieldKey) so the migration and every
    /// seeded store agree without literal GUID tables.</summary>
    public static Guid StableId(RecordType type, string fieldKey) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"fieldregistry:{type}:{fieldKey}")));

    public static readonly IReadOnlyList<Row> Rows =
    [
        // Requisition — SourcingModels.RequisitionDto (14)
        new(RecordType.Requisition, "Code", FieldDataType.Code, "PR #"),
        new(RecordType.Requisition, "Requestor", FieldDataType.Text, "Requestor"),
        new(RecordType.Requisition, "Department", FieldDataType.Text, "Department"),
        new(RecordType.Requisition, "Location", FieldDataType.Text, "Location"),
        new(RecordType.Requisition, "Memo", FieldDataType.Text, "Memo"),
        new(RecordType.Requisition, "Job", FieldDataType.Text, "Job"),
        new(RecordType.Requisition, "Category", FieldDataType.Text, "Category"),
        new(RecordType.Requisition, "CostCentre", FieldDataType.Text, "Cost centre"),
        new(RecordType.Requisition, "Project", FieldDataType.Text, "Project"),
        new(RecordType.Requisition, "RaisedDate", FieldDataType.Date, "Raised"),
        new(RecordType.Requisition, "RequiredDate", FieldDataType.Date, "Required by"),
        new(RecordType.Requisition, "HeaderStatus", FieldDataType.Enum, "PR status"),
        new(RecordType.Requisition, "Value", FieldDataType.Money, "Value"),
        new(RecordType.Requisition, "Submitted", FieldDataType.Bool, "Submitted"),

        // Rfq — SourcingModels.RfqListItem (10)
        new(RecordType.Rfq, "Code", FieldDataType.Code, "RFQ"),
        new(RecordType.Rfq, "Title", FieldDataType.Text, "Title"),
        new(RecordType.Rfq, "Envelope", FieldDataType.Enum, "Envelope"),
        new(RecordType.Rfq, "Status", FieldDataType.Enum, "Status"),
        new(RecordType.Rfq, "Currency", FieldDataType.Code, "Currency"),
        new(RecordType.Rfq, "ClosesUtc", FieldDataType.Instant, "Closes"),
        new(RecordType.Rfq, "InvitedCount", FieldDataType.Number, "Vendors"),
        new(RecordType.Rfq, "LineCount", FieldDataType.Number, "Lines"),
        new(RecordType.Rfq, "QuestionCount", FieldDataType.Number, "Questions"),
        new(RecordType.Rfq, "BidCount", FieldDataType.Number, "Bids"),

        // PurchaseOrder — PoModels.PoListItem (9)
        new(RecordType.PurchaseOrder, "Code", FieldDataType.Code, "PO"),
        new(RecordType.PurchaseOrder, "VendorName", FieldDataType.Text, "Vendor"),
        new(RecordType.PurchaseOrder, "RfqCode", FieldDataType.Code, "From RFQ"),
        new(RecordType.PurchaseOrder, "Status", FieldDataType.Enum, "Status"),
        new(RecordType.PurchaseOrder, "Total", FieldDataType.Money, "Value"),
        new(RecordType.PurchaseOrder, "ReceivedQty", FieldDataType.Number, "Received qty"),
        new(RecordType.PurchaseOrder, "TotalQty", FieldDataType.Number, "Total qty"),
        new(RecordType.PurchaseOrder, "Acknowledged", FieldDataType.Bool, "Acknowledged"),
        new(RecordType.PurchaseOrder, "NsId", FieldDataType.Code, "NetSuite id"),

        // Invoice — InvoiceModels.InvoiceListDto (9)
        new(RecordType.Invoice, "Code", FieldDataType.Code, "Invoice"),
        new(RecordType.Invoice, "PoCode", FieldDataType.Code, "PO"),
        new(RecordType.Invoice, "VendorName", FieldDataType.Text, "Vendor"),
        new(RecordType.Invoice, "InvoiceNo", FieldDataType.Text, "Supplier ref"),
        new(RecordType.Invoice, "Status", FieldDataType.Enum, "Status"),
        new(RecordType.Invoice, "MatchStatus", FieldDataType.Enum, "Match"),
        new(RecordType.Invoice, "Subtotal", FieldDataType.Money, "Subtotal"),
        new(RecordType.Invoice, "Total", FieldDataType.Money, "Total"),
        new(RecordType.Invoice, "Payable", FieldDataType.Bool, "Payable"),

        // Grn — DeliveryModels.GrnDetailDto (CF-FIX4-T2; no list DTO yet — detail is the contract)
        new(RecordType.Grn, "Code", FieldDataType.Code, "GRN"),
        new(RecordType.Grn, "AsnCode", FieldDataType.Code, "ASN"),
        new(RecordType.Grn, "PoCode", FieldDataType.Code, "PO"),
        new(RecordType.Grn, "ReceivedDate", FieldDataType.Date, "Received"),
        new(RecordType.Grn, "NsId", FieldDataType.Code, "NetSuite id"),

        // Asn — DeliveryModels.AsnListDto (7)
        new(RecordType.Asn, "Code", FieldDataType.Code, "ASN"),
        new(RecordType.Asn, "PoCode", FieldDataType.Code, "PO"),
        new(RecordType.Asn, "VendorName", FieldDataType.Text, "Vendor"),
        new(RecordType.Asn, "Carrier", FieldDataType.Text, "Carrier"),
        new(RecordType.Asn, "ExpectedDate", FieldDataType.Date, "Expected"),
        new(RecordType.Asn, "Status", FieldDataType.Enum, "Status"),
        new(RecordType.Asn, "GrnCode", FieldDataType.Code, "GRN"),

        // Vendor — VendorModels.VendorListItem (9)
        new(RecordType.Vendor, "Code", FieldDataType.Code, "Vendor #"),
        new(RecordType.Vendor, "Name", FieldDataType.Text, "Vendor"),
        new(RecordType.Vendor, "Type", FieldDataType.Enum, "Type"),
        new(RecordType.Vendor, "Categories", FieldDataType.Tags, "SWEC codes"),
        new(RecordType.Vendor, "Region", FieldDataType.Enum, "Region"),
        new(RecordType.Vendor, "State", FieldDataType.Text, "State"),
        new(RecordType.Vendor, "Rating", FieldDataType.Number, "Rating"),
        new(RecordType.Vendor, "Otd", FieldDataType.Number, "OTD %"),
        new(RecordType.Vendor, "Status", FieldDataType.Enum, "Status"),

        // Onboarding — OnboardingModels.OnboardingQueueItemDto (9)
        new(RecordType.Onboarding, "Code", FieldDataType.Code, "Application"),
        new(RecordType.Onboarding, "Name", FieldDataType.Text, "Vendor"),
        new(RecordType.Onboarding, "Type", FieldDataType.Enum, "Type"),
        new(RecordType.Onboarding, "Status", FieldDataType.Enum, "Status"),
        new(RecordType.Onboarding, "Source", FieldDataType.Enum, "Source"),
        new(RecordType.Onboarding, "CreatedUtc", FieldDataType.Instant, "Created"),
        new(RecordType.Onboarding, "SubmittedUtc", FieldDataType.Instant, "Received"),
        new(RecordType.Onboarding, "OpenRoundNo", FieldDataType.Number, "Open round"),
        new(RecordType.Onboarding, "RoundCount", FieldDataType.Number, "Rounds"),
    ];

    public static IEnumerable<FieldRegistryEntry> ToEntities() =>
        Rows.Select(r => new FieldRegistryEntry
        {
            Id = StableId(r.RecordType, r.FieldKey),
            RecordType = r.RecordType,
            FieldKey = r.FieldKey,
            Kind = FieldKind.Native,
            Label = r.Label,
            DataType = r.DataType,
        });
}

using System.Security.Cryptography;
using System.Text;
using eProcure.Domain.Views;

namespace eProcure.Application.Views;

/// <summary>
/// D7.5 (a-parity, ruled): one system view per flat rollout mount, columns in the native
/// table's on-screen order with NO filters and NO sort — each default reproduces today's
/// list exactly (the VIEW-SYS discipline; the crawl proves parity per screen).
/// Codes start at 0010: 0001 is D3's All RFQs and 0002 is D4's Recent purchase orders.
/// Requisitions deliberately has NO system view: its rows don't project to view columns
/// (lines, bulk-select) — the picker there is an id-intersection filter and the native
/// list stays the default (an honest asymmetry beats a fake symmetry, ruled).
/// </summary>
public static class SystemViewSeed
{
    public sealed record Row(string Code, string Name, RecordType RecordType, string[] Columns);

    public static readonly IReadOnlyList<Row> Rows =
    [
        new("VIEW-SYS-0010", "All Purchase Orders", RecordType.PurchaseOrder,
            ["Code", "VendorName", "RfqCode", "Total", "ReceivedQty", "TotalQty", "Status"]),
        new("VIEW-SYS-0011", "All Vendors", RecordType.Vendor,
            ["Code", "Name", "Type", "Categories", "Region", "State", "Rating", "Otd", "Status"]),
        new("VIEW-SYS-0012", "All Invoices", RecordType.Invoice,
            ["Code", "InvoiceNo", "PoCode", "Total", "MatchStatus", "Status"]),
        new("VIEW-SYS-0013", "All Shipping Notices", RecordType.Asn,
            ["Code", "PoCode", "Carrier", "ExpectedDate", "Status"]),
    ];

    // HEX-PARSE (the SegmentSeed rule): identical ids from C# and SQL derivations.
    private static Guid HexGuid(string input) =>
        Guid.Parse(Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(input))));

    public static Guid ViewId(string code) => HexGuid($"systemview:{code}");
    public static Guid ColumnId(Guid viewId, string fieldKey) => HexGuid($"systemviewcol:{viewId}:{fieldKey}");
}

using eProcure.Domain.Procurement;

namespace eProcure.Infrastructure.Seed;

/// <summary>
/// Dummy procure-to-pay demo data (SEED-DATA §10–12): POs across states, ASNs/GRNs,
/// and invoices (one Paid, one Submitted/Matched, one Exception). NetSuite NsIds are
/// cosmetic (integration stubbed).
/// </summary>
public static class ProcurementSeed
{
    public sealed record PoSeedRow(
        string Code, string VendorCode, PoStatus Status, bool Ack, string NsId, string Incoterm,
        (string Code, string Desc, decimal Qty, string Uom, decimal Price, decimal Recv, decimal Inv)[] Lines);

    public static readonly PoSeedRow[] Pos =
    [
        new("PO-2026-1185", "SWK-V-10293", PoStatus.Matched, true, "NS-PO-44812", "DDP Bintulu",
            [("MEP-PUMP-075", "Centrifugal Pump, 75 kW, end-suction", 4, "Unit", 48500, 4, 4),
             ("ELE-VFD-075", "VFD Drive, 75 kW, IP55", 4, "Unit", 18200, 4, 4)]),
        new("PO-2026-1186", "SWK-V-10410", PoStatus.PartiallyReceived, true, "NS-PO-44813", "DDP Samalaju",
            [("VLV-GAT-150", "Gate Valve, DN150, PN16, CS", 24, "Unit", 980, 16, 0)]),
        new("PO-2026-1190", "SWK-V-10781", PoStatus.Issued, false, "NS-PO-44820", "DDP Bintulu",
            [("MRO-LOT-01", "MRO consumables (annual basket)", 1, "Lot", 39000, 0, 0)]),
        new("PO-2026-1193", "SWK-V-11002", PoStatus.Discrepancy, true, "NS-PO-44827", "DDP Samalaju",
            [("ELE-MTR-200", "Motor, 200 kW, TEFC", 6, "Unit", 22500, 6, 6)]),
        new("PO-2026-1199", "SWK-V-11890", PoStatus.Acknowledged, true, "NS-PO-44831", "DDP Bintulu",
            [("ELE-MOT-055", "LV Motor, 55 kW, IE3", 3, "Unit", 9600, 0, 0)]),
    ];

    public static PurchaseOrder ToPo(PoSeedRow r, Guid vendorId, DateTime now) => new PurchaseOrder
    {
        Code = r.Code,
        VendorId = vendorId,
        Acknowledged = r.Ack,
        NsId = r.NsId,
        Incoterm = r.Incoterm,
        Currency = "MYR",
        CreatedUtc = now,
        UpdatedUtc = now,
        Lines = r.Lines.Select(l => new PoLine
        {
            ItemCode = l.Code, Description = l.Desc, Qty = l.Qty, Uom = l.Uom,
            UnitPrice = l.Price, ReceivedQty = l.Recv, InvoicedQty = l.Inv,
        }).ToList(),
    }.SeededAs(r.Status);
}

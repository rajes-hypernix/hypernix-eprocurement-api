namespace eProcure.Infrastructure.Persistence;

/// <summary>
/// The one-time Award→PO lineage backfill (Slice H T2, AN-2), extracted from the
/// <c>AwardPoLineage</c> migration so the SAME statements can be exercised by a test against real
/// rows. Two derivations, both fail-safe (never guess):
///   1. PO → Award: match the legacy <c>AwardCode</c> string to <c>Award.Code</c>. Unmatched POs keep
///      <c>AwardId</c> null.
///   2. PO line → AwardAllocation: a line maps to the allocation with the same award (via the PO's
///      backfilled <c>AwardId</c>), vendor and RFQ line code. Set ONLY where that match is unique; a
///      vendor with more than one allocation for the same line code is ambiguous and stays null.
/// Statement 2 depends on statement 1 having run first (it reads <c>PurchaseOrders.AwardId</c>).
/// </summary>
public static class AwardPoLineageBackfill
{
    public static readonly string[] Statements =
    [
        // 1. PO → Award via the legacy AwardCode.
        @"UPDATE ""PurchaseOrders"" po SET ""AwardId"" = a.""Id""
          FROM ""Awards"" a WHERE po.""AwardCode"" = a.""Code"";",

        // 2. PO line → AwardAllocation, only where the (award, vendor, line code) match is unique.
        @"UPDATE ""PoLines"" pl SET ""AwardAllocationId"" = m.aid
          FROM (
              SELECT pl2.""Id"" AS plid, (array_agg(aa.""Id""))[1] AS aid, count(*) AS n
              FROM ""PoLines"" pl2
              JOIN ""PurchaseOrders"" po ON po.""Id"" = pl2.""PurchaseOrderId""
              JOIN ""AwardAllocations"" aa
                ON aa.""AwardId"" = po.""AwardId"" AND aa.""VendorId"" = po.""VendorId"" AND aa.""RfqLineCode"" = pl2.""ItemCode""
              WHERE po.""AwardId"" IS NOT NULL
              GROUP BY pl2.""Id""
          ) m
          WHERE pl.""Id"" = m.plid AND m.n = 1;",
    ];
}

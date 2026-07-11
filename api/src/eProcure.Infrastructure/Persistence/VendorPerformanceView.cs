namespace eProcure.Infrastructure.Persistence;

/// <summary>
/// The keyless read-model row backing the <c>VendorPerformanceView</c> SQL view (Slice H T7, AN-4/
/// DBA-10). VendorPerformance is no longer STORED (it could drift from the facts); it is DERIVED from
/// GRN / RfqInvitation / Award / Invoice / PO facts. Metrics that cannot be computed from a real fact
/// return NULL ("not yet available") — never a stale or fabricated number:
///   • Otd — NULL: no promised/expected delivery date exists on PO/PO line to measure against.
///   • Breaches — NULL: a short receipt is a delivery shortfall, not a modelled compliance breach.
///   • Quality/Response/WinRate — NULL when the denominator is zero (no receipts / invitations).
///   • Lead — actual avg DAYS from PO.IssuedUtc to Grn.ReceivedDate over pairs where both exist; NULL
///     otherwise. (Not the vendor's self-reported Bid.Lead.) Days, not the legacy weeks int.
/// </summary>
public sealed class VendorPerformanceRow
{
    public Guid VendorId { get; set; }
    public int? Otd { get; set; }
    public int? Quality { get; set; }
    public int? Breaches { get; set; }
    public int? LeadDays { get; set; }
    public int? Response { get; set; }
    public int? WinRate { get; set; }
    public decimal SpendYtd { get; set; }
    public int Pos { get; set; }
}

/// <summary>The view SQL — one source of truth, referenced by the migration (create + Down re-derive).</summary>
public static class VendorPerformanceView
{
    public const string Name = "VendorPerformanceView";

    public const string CreateSql = $@"
        CREATE VIEW ""{Name}"" AS
        SELECT
            v.""Id"" AS ""VendorId"",
            NULL::int AS ""Otd"",        -- no promised-delivery-date fact to measure against
            NULL::int AS ""Breaches"",   -- short receipts are not modelled compliance breaches
            -- Quality: % of GRN lines received Good (rolling 12 months)
            (SELECT round(100.0 * count(*) FILTER (WHERE gl.""Condition"" = 'Good') / nullif(count(*), 0))::int
               FROM ""Grns"" g
               JOIN ""GrnLines"" gl ON gl.""GrnId"" = g.""Id""
               JOIN ""PurchaseOrders"" po ON po.""Id"" = g.""PoId""
               WHERE po.""VendorId"" = v.""Id"" AND g.""ReceivedDate"" >= CURRENT_DATE - INTERVAL '12 months') AS ""Quality"",
            -- Lead: actual avg DAYS from PO issued to goods received, over pairs where both instants exist
            (SELECT round(avg(g.""ReceivedDate"" - (po.""IssuedUtc"" AT TIME ZONE 'UTC')::date))::int
               FROM ""Grns"" g
               JOIN ""PurchaseOrders"" po ON po.""Id"" = g.""PoId""
               WHERE po.""VendorId"" = v.""Id"" AND po.""IssuedUtc"" IS NOT NULL AND g.""ReceivedDate"" IS NOT NULL) AS ""LeadDays"",
            -- Response: % of invitations the vendor responded to (rolling 12 months)
            (SELECT round(100.0 * count(*) FILTER (WHERE ri.""RespondedUtc"" IS NOT NULL) / nullif(count(*), 0))::int
               FROM ""RfqInvitations"" ri
               WHERE ri.""VendorId"" = v.""Id"" AND ri.""InvitedUtc"" >= CURRENT_DATE - INTERVAL '12 months') AS ""Response"",
            -- WinRate: approved awards won / invitations (rolling 12 months)
            (SELECT round(100.0 *
                 (SELECT count(DISTINCT aw.""Id"") FROM ""Awards"" aw
                    JOIN ""AwardAllocations"" aa ON aa.""AwardId"" = aw.""Id""
                    WHERE aa.""VendorId"" = v.""Id"" AND aw.""Status"" = 'Approved'
                      AND aw.""ApprovedUtc"" >= CURRENT_DATE - INTERVAL '12 months')
                 / nullif((SELECT count(*) FROM ""RfqInvitations"" ri
                             WHERE ri.""VendorId"" = v.""Id"" AND ri.""InvitedUtc"" >= CURRENT_DATE - INTERVAL '12 months'), 0))::int) AS ""WinRate"",
            -- SpendYtd: net (pre-tax) value of Paid invoices this calendar year
            (SELECT coalesce(sum(il.""Qty"" * il.""UnitPrice""), 0)
               FROM ""Invoices"" i
               JOIN ""InvoiceLines"" il ON il.""InvoiceId"" = i.""Id""
               WHERE i.""VendorId"" = v.""Id"" AND i.""Status"" = 'Paid'
                 AND i.""Date"" >= (date_trunc('year', CURRENT_DATE))::date)::numeric(18,2) AS ""SpendYtd"",
            -- Pos: count of non-Draft purchase orders
            (SELECT count(*) FROM ""PurchaseOrders"" po WHERE po.""VendorId"" = v.""Id"" AND po.""Status"" <> 'Draft')::int AS ""Pos""
        FROM ""Vendors"" v;";

    public const string DropSql = $@"DROP VIEW IF EXISTS ""{Name}"";";
}

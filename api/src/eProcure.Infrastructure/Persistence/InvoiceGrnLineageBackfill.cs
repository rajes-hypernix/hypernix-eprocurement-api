namespace eProcure.Infrastructure.Persistence;

/// <summary>
/// The one-time Invoice→GRN lineage backfill (Slice H T3), extracted from the <c>InvoiceGrnLineage</c>
/// migration so the SAME statement can be exercised by a test against real rows. An invoice links to
/// its receipt only where the invoice's PO has EXACTLY ONE GRN; a PO with multiple partial receipts is
/// ambiguous (we can't tell which receipt the invoice matches), so those invoices stay null — never
/// guessed. This completes the three-way-match lineage (PO · receipt · invoice).
/// </summary>
public static class InvoiceGrnLineageBackfill
{
    public static readonly string[] Statements =
    [
        @"UPDATE ""Invoices"" i SET ""GrnId"" = m.gid
          FROM (
              SELECT i2.""Id"" AS iid, (array_agg(g.""Id""))[1] AS gid, count(*) AS n
              FROM ""Invoices"" i2
              JOIN ""Grns"" g ON g.""PoId"" = i2.""PoId""
              GROUP BY i2.""Id""
          ) m
          WHERE i.""Id"" = m.iid AND m.n = 1;",
    ];
}

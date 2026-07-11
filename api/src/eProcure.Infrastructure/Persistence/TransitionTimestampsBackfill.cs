namespace eProcure.Infrastructure.Persistence;

/// <summary>
/// The one-time transition-timestamp backfill (Slice H T5, AN-3), extracted from the
/// <c>TransitionTimestamps</c> migration so the SAME statements can be exercised by a test. Only the
/// columns with a CLEAN recorded source are backfilled; a null means "we don't reliably know when",
/// never "now" (T5 ruling: null over fabrication for anything below a clean source):
///   • Rfq.ReleasedUtc / ClosedUtc  ← the typed RfqEvent (EventType 'Released' / 'Closed', OccurredUtc).
///   • PurchaseRequisition.SubmittedUtc ← the typed transition AuditEntry (WriteTransitionAsync wrote
///     EntityType 'PurchaseRequisition', ToState 'Submitted', on the PR's Code).
/// Deliberately LEFT NULL (no clean source — only generic Action-string audit rows, rated below clean):
///   Rfq.AwardedUtc, PurchaseOrder.IssuedUtc / AcknowledgedUtc, Invoice.SubmittedUtc / ApprovedUtc,
///   Asn.ReceivedUtc. Going forward these are stamped inside the domain transition methods via IClock;
///   historical cycle-time for them is partial, and that is the honest outcome.
/// </summary>
public static class TransitionTimestampsBackfill
{
    public static readonly string[] Statements =
    [
        // 1. RFQ released — from the typed RfqEvent (earliest, in case of anomalies).
        @"UPDATE ""Rfqs"" r SET ""ReleasedUtc"" = e.t
          FROM (SELECT ""RfqId"", min(""OccurredUtc"") AS t FROM ""RfqEvents"" WHERE ""EventType"" = 'Released' GROUP BY ""RfqId"") e
          WHERE r.""Id"" = e.""RfqId"";",

        // 2. RFQ closed (early close) — from the typed RfqEvent.
        @"UPDATE ""Rfqs"" r SET ""ClosedUtc"" = e.t
          FROM (SELECT ""RfqId"", min(""OccurredUtc"") AS t FROM ""RfqEvents"" WHERE ""EventType"" = 'Closed' GROUP BY ""RfqId"") e
          WHERE r.""Id"" = e.""RfqId"";",

        // 3. PR submitted — from the typed transition AuditEntry (EntityId is the PR Code).
        @"UPDATE ""PurchaseRequisitions"" pr SET ""SubmittedUtc"" = a.t
          FROM (SELECT ""EntityId"", min(""UtcTimestamp"") AS t FROM ""AuditEntries""
                WHERE ""EntityType"" = 'PurchaseRequisition' AND ""ToState"" = 'Submitted' GROUP BY ""EntityId"") a
          WHERE pr.""Code"" = a.""EntityId"";",
    ];
}

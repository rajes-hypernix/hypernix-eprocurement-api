namespace eProcure.Application.Abstractions;

/// <summary>
/// Outbound ERP integration. Integration is OFF for now — the only implementation
/// is a no-op/log stub (CLAUDE.md "Integration scope"). When enabled (Slice 11),
/// these pushes become queued + retryable; never silently assumed successful.
/// </summary>
public interface INetSuiteClient
{
    Task PushPurchaseOrderAsync(string poCode, CancellationToken ct = default);
    Task PushVendorBillAsync(string invoiceCode, CancellationToken ct = default);
    Task PushItemReceiptAsync(string grnCode, CancellationToken ct = default);
    Task PushBillPaymentAsync(string voucherCode, CancellationToken ct = default);
}

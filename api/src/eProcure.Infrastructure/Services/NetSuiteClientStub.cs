using eProcure.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace eProcure.Infrastructure.Services;

/// <summary>
/// No-op NetSuite client. Integration is OFF for now (CLAUDE.md). Each call logs
/// the intended push and returns; no external calls are made. Replaced by a
/// queued/retryable client in Slice 11.
/// </summary>
public sealed class NetSuiteClientStub(ILogger<NetSuiteClientStub> logger) : INetSuiteClient
{
    public Task PushPurchaseOrderAsync(string poCode, CancellationToken ct = default)
        => Log("PurchaseOrder", poCode);

    public Task PushVendorBillAsync(string invoiceCode, CancellationToken ct = default)
        => Log("VendorBill", invoiceCode);

    public Task PushItemReceiptAsync(string grnCode, CancellationToken ct = default)
        => Log("ItemReceipt", grnCode);

    public Task PushBillPaymentAsync(string voucherCode, CancellationToken ct = default)
        => Log("BillPayment", voucherCode);

    private Task Log(string recordType, string code)
    {
        logger.LogInformation(
            "[NetSuite STUB] Would push {RecordType} {Code} (integration disabled).",
            recordType, code);
        return Task.CompletedTask;
    }
}

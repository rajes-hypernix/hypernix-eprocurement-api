using FSH.Modules.Procurement.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Procurement.Contracts.v1.Invoices;

public sealed record SubmitInvoiceCommand(
    Guid PoId,
    string InvoiceNo,
    DateOnly? Date,
    decimal WhtRate,
    IReadOnlyList<InvoiceLineInput> Lines) : ICommand<InvoiceDto>;

public sealed record InvoiceLineInput(string ItemCode, decimal Qty, decimal UnitPrice);

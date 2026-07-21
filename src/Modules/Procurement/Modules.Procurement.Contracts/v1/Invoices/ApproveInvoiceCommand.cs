using FSH.Modules.Procurement.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Procurement.Contracts.v1.Invoices;

public sealed record ApproveInvoiceCommand(Guid InvoiceId) : ICommand<InvoiceDto>;

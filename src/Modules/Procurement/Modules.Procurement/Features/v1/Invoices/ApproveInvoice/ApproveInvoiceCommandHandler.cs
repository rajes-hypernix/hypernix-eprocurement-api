using FSH.Framework.Core.Exceptions;
using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.Invoices;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.Invoices.ApproveInvoice;

public sealed class ApproveInvoiceCommandHandler(ProcurementDbContext dbContext)
    : ICommandHandler<ApproveInvoiceCommand, InvoiceDto>
{
    public async ValueTask<InvoiceDto> Handle(ApproveInvoiceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var invoice = await dbContext.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == command.InvoiceId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Invoice {command.InvoiceId} not found.");

        invoice.Approve();

        var po = await dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == invoice.PoId, cancellationToken)
            .ConfigureAwait(false);

        if (po is not null)
        {
            bool allMatched = po.Lines.All(l => l.InvoicedQty >= l.Qty);
            if (allMatched)
                po.MarkMatched();
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ProcurementDtoMapper.ToDto(invoice);
    }
}

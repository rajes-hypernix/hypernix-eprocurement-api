using FSH.Framework.Core.Exceptions;
using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.Invoices;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.Invoices.ResolveInvoiceException;

public sealed class ResolveInvoiceExceptionCommandHandler(ProcurementDbContext dbContext)
    : ICommandHandler<ResolveInvoiceExceptionCommand, InvoiceDto>
{
    public async ValueTask<InvoiceDto> Handle(ResolveInvoiceExceptionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var invoice = await dbContext.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == command.InvoiceId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Invoice {command.InvoiceId} not found.");

        invoice.ResolveException();

        var po = await dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == invoice.PoId, cancellationToken)
            .ConfigureAwait(false);

        if (po is not null && po.Status == PoStatus.Discrepancy)
        {
            bool anyOtherExceptions = await dbContext.Invoices
                .AnyAsync(i => i.PoId == po.Id && i.Id != invoice.Id && i.Status == InvoiceStatus.Exception, cancellationToken)
                .ConfigureAwait(false);

            if (!anyOtherExceptions)
                po.ClearDiscrepancy();
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ProcurementDtoMapper.ToDto(invoice);
    }
}

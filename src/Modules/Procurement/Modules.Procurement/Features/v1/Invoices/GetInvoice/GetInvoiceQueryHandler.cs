using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.Invoices;
using FSH.Modules.Procurement.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.Invoices.GetInvoice;

public sealed class GetInvoiceQueryHandler(ProcurementDbContext dbContext)
    : IQueryHandler<GetInvoiceQuery, InvoiceDto?>
{
    public async ValueTask<InvoiceDto?> Handle(GetInvoiceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var invoice = await dbContext.Invoices
            .AsNoTracking()
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == query.InvoiceId, cancellationToken)
            .ConfigureAwait(false);

        return invoice is null ? null : ProcurementDtoMapper.ToDto(invoice);
    }
}

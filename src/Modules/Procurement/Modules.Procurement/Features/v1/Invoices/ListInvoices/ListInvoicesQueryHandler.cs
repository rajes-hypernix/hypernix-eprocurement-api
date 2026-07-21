using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.Invoices;
using FSH.Modules.Procurement.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.Invoices.ListInvoices;

public sealed class ListInvoicesQueryHandler(ProcurementDbContext dbContext)
    : IQueryHandler<ListInvoicesQuery, IReadOnlyList<InvoiceDto>>
{
    public async ValueTask<IReadOnlyList<InvoiceDto>> Handle(ListInvoicesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var invoices = await dbContext.Invoices
            .AsNoTracking()
            .Include(i => i.Lines)
            .OrderByDescending(i => i.CreatedUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. invoices.Select(ProcurementDtoMapper.ToDto)];
    }
}

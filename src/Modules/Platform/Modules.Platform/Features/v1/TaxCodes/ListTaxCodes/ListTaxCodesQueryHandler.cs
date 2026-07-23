using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.TaxCodes.ListTaxCodes;

public sealed class ListTaxCodesQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<ListTaxCodesQuery, IReadOnlyList<TaxCodeDto>>
{
    public async ValueTask<IReadOnlyList<TaxCodeDto>> Handle(ListTaxCodesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var q = dbContext.TaxCodes.AsNoTracking().AsQueryable();
        if (query.ActiveOnly)
            q = q.Where(t => t.IsActive);

        return await q.OrderBy(t => t.Code)
            .Select(t => new TaxCodeDto(t.Id, t.Code, t.Name, t.RatePct, t.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

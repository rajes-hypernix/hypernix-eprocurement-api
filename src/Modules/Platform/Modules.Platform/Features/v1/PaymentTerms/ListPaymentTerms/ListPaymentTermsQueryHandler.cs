using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.PaymentTerms.ListPaymentTerms;

public sealed class ListPaymentTermsQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<ListPaymentTermsQuery, IReadOnlyList<PaymentTermDto>>
{
    public async ValueTask<IReadOnlyList<PaymentTermDto>> Handle(
        ListPaymentTermsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var q = dbContext.PaymentTerms.AsNoTracking().AsQueryable();
        if (query.ActiveOnly)
            q = q.Where(t => t.IsActive);

        var terms = await q.OrderBy(t => t.Code).ToListAsync(cancellationToken).ConfigureAwait(false);
        return [.. terms.Select(ConfigurationMapping.ToDto)];
    }
}

using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Banks;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Banks.ListBanks;

public sealed class ListBanksQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<ListBanksQuery, IReadOnlyList<BankDto>>
{
    public async ValueTask<IReadOnlyList<BankDto>> Handle(ListBanksQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var q = dbContext.Banks.AsNoTracking().AsQueryable();
        if (query.ActiveOnly)
            q = q.Where(b => b.IsActive);
        if (!string.IsNullOrWhiteSpace(query.CountryCode))
        {
            var code = query.CountryCode.Trim().ToUpperInvariant();
            q = q.Where(b => b.CountryCode == code);
        }

        return await q.OrderBy(b => b.Name)
            .Select(b => new BankDto(b.Id, b.Name, b.SwiftCode, b.CountryCode, b.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

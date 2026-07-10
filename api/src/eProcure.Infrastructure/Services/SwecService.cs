using eProcure.Application.Suppliers;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

public sealed class SwecService(AppDbContext db) : ISwecService
{
    public async Task<IReadOnlyList<SwecCategoryDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await db.SwecCategories.AsNoTracking().OrderBy(c => c.Code).ToListAsync(ct);
        return rows.Select(c => new SwecCategoryDto(
            c.Code, c.Name, c.ParentCode, c.Level, c.IsLeaf, c.PathText)).ToList();
    }
}

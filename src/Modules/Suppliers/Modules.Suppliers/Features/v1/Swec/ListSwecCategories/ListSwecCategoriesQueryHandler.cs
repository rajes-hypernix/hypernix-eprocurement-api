using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.Swec;
using FSH.Modules.Suppliers.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Features.v1.Swec.ListSwecCategories;

public sealed class ListSwecCategoriesQueryHandler(SuppliersDbContext dbContext)
    : IQueryHandler<ListSwecCategoriesQuery, IReadOnlyList<SwecCategoryDto>>
{
    public async ValueTask<IReadOnlyList<SwecCategoryDto>> Handle(ListSwecCategoriesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await dbContext.SwecCategories
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .Select(c => new SwecCategoryDto(c.Code, c.Name, c.ParentCode, c.Level, c.IsLeaf, c.PathText))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

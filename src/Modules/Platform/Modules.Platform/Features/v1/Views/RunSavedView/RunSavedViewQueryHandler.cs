using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.Services;
using FSH.Modules.Platform.Contracts.v1.Views;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Views.RunSavedView;

public sealed class RunSavedViewQueryHandler(PlatformDbContext dbContext, ICurrentUser currentUser, IEnumerable<ISavedViewRowSource> rowSources)
    : IQueryHandler<RunSavedViewQuery, ViewRunResult>
{
    private const int MaxSize = 200;

    public async ValueTask<ViewRunResult> Handle(RunSavedViewQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var view = await dbContext.SavedViews
            .AsNoTracking()
            .Include(v => v.Filters)
            .Include(v => v.Columns)
            .FirstOrDefaultAsync(v => v.Id == query.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Saved view {query.Id} not found.");

        string userId = currentUser.GetUserId().ToString();
        if (!view.IsSystem && !view.IsShared && view.OwnerUserId != userId)
            throw new ForbiddenException("You do not have access to this saved view.");

        string recordType = view.RecordType.ToString();
        IReadOnlyList<IDictionary<string, object?>>? rows = null;
        foreach (var source in rowSources)
        {
            try
            {
                rows = await source.GetRowsAsync(recordType, cancellationToken).ConfigureAwait(false);
                break;
            }
            catch (NotSupportedException)
            {
                // This source doesn't own the record type — try the next registered source.
            }
        }

        rows ??= [];
        var filtered = SavedViewFilterExecutor.Apply(rows, view.Filters);

        int size = Math.Clamp(query.Size <= 0 ? 50 : query.Size, 1, MaxSize);
        int page = Math.Max(query.Page, 1);
        var paged = filtered
            .Skip((page - 1) * size)
            .Take(size)
            .Select(row => Project(row, view.Columns))
            .ToList();

        return new ViewRunResult(paged, filtered.Count, page, size);
    }

    private static IDictionary<string, object?> Project(IDictionary<string, object?> row, IReadOnlyList<SavedViewColumn> columns)
    {
        var result = new Dictionary<string, object?>();
        if (row.TryGetValue("Id", out var id))
            result["Id"] = id;

        foreach (var column in columns.OrderBy(c => c.Sort))
            result[column.FieldKey] = row.TryGetValue(column.FieldKey, out var value) ? value : null;

        return result;
    }
}

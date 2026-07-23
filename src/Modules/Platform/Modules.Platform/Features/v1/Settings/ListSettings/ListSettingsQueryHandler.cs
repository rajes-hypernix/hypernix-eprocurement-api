using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Settings.ListSettings;

public sealed class ListSettingsQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<ListSettingsQuery, IReadOnlyList<SettingDto>>
{
    public async ValueTask<IReadOnlyList<SettingDto>> Handle(ListSettingsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await dbContext.Settings.AsNoTracking()
            .OrderBy(s => s.Key)
            .Select(s => new SettingDto(s.Id, s.Key, s.Value, s.ValueKind, s.Label, s.Description))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

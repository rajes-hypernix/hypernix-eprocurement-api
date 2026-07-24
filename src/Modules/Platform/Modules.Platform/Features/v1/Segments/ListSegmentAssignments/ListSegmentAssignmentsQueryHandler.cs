using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Segments;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Segments.ListSegmentAssignments;

public sealed class ListSegmentAssignmentsQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<ListSegmentAssignmentsQuery, IReadOnlyList<SegmentAssignmentDto>>
{
    public async ValueTask<IReadOnlyList<SegmentAssignmentDto>> Handle(ListSegmentAssignmentsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var recordType = CustomFieldMapping.ParseEnum<PlatformRecordType>(query.RecordType, "record type");

        var assignments = await dbContext.SegmentAssignments
            .AsNoTracking()
            .Where(a => a.RecordType == recordType && a.RecordId == query.RecordId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (assignments.Count == 0)
        {
            return [];
        }

        var orgUnitIds = assignments.Select(a => a.OrgUnitId).Distinct().ToList();
        var orgUnits = await dbContext.OrgUnits
            .AsNoTracking()
            .Where(o => orgUnitIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, cancellationToken)
            .ConfigureAwait(false);

        return [.. assignments.Select(a => new SegmentAssignmentDto(
            a.Id,
            a.Dimension.ToString(),
            a.OrgUnitId,
            orgUnits.TryGetValue(a.OrgUnitId, out var ou) ? ou.Code : string.Empty,
            orgUnits.TryGetValue(a.OrgUnitId, out var ou2) ? ou2.Name : string.Empty,
            a.LineId))];
    }
}

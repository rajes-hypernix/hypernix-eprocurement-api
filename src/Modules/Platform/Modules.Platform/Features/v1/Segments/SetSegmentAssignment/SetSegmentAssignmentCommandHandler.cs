using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.Segments;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Segments.SetSegmentAssignment;

public sealed class SetSegmentAssignmentCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<SetSegmentAssignmentCommand, Guid>
{
    public async ValueTask<Guid> Handle(SetSegmentAssignmentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var recordType = CustomFieldMapping.ParseEnum<PlatformRecordType>(command.RecordType, "record type");
        var dimension = CustomFieldMapping.ParseEnum<OrgUnitType>(command.Dimension, "segment dimension");

        var orgUnit = await dbContext.OrgUnits
            .FirstOrDefaultAsync(o => o.Id == command.OrgUnitId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Org unit {command.OrgUnitId} not found.");

        if (orgUnit.Type != dimension)
        {
            throw new PlatformRuleException($"{orgUnit.Code} is a {orgUnit.Type} value, not {dimension}.");
        }

        var assignment = await dbContext.SegmentAssignments
            .FirstOrDefaultAsync(a =>
                a.RecordType == recordType && a.RecordId == command.RecordId && a.LineId == command.LineId && a.Dimension == dimension,
                cancellationToken)
            .ConfigureAwait(false);

        if (assignment is null)
        {
            assignment = SegmentAssignment.Create(recordType, command.RecordId, command.LineId, dimension, command.OrgUnitId);
            dbContext.SegmentAssignments.Add(assignment);
        }
        else
        {
            assignment.Reassign(command.OrgUnitId);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return assignment.Id;
    }
}

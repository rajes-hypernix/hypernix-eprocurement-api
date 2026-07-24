using FSH.Modules.Platform.Contracts.v1.Segments;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Segments.RemoveSegmentAssignment;

public sealed class RemoveSegmentAssignmentCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<RemoveSegmentAssignmentCommand>
{
    public async ValueTask<Unit> Handle(RemoveSegmentAssignmentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var recordType = CustomFieldMapping.ParseEnum<PlatformRecordType>(command.RecordType, "record type");
        var dimension = CustomFieldMapping.ParseEnum<OrgUnitType>(command.Dimension, "segment dimension");

        var assignment = await dbContext.SegmentAssignments
            .FirstOrDefaultAsync(a =>
                a.RecordType == recordType && a.RecordId == command.RecordId && a.LineId == command.LineId && a.Dimension == dimension,
                cancellationToken)
            .ConfigureAwait(false);

        if (assignment is not null)
        {
            dbContext.SegmentAssignments.Remove(assignment);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Unit.Value;
    }
}

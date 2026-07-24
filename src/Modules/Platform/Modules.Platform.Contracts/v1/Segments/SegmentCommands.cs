using FSH.Modules.Platform.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Platform.Contracts.v1.Segments;

public sealed record ListSegmentAssignmentsQuery(string RecordType, Guid RecordId) : IQuery<IReadOnlyList<SegmentAssignmentDto>>;

public sealed record SetSegmentAssignmentCommand(string RecordType, Guid RecordId, Guid? LineId, string Dimension, Guid OrgUnitId) : ICommand<Guid>;

public sealed record RemoveSegmentAssignmentCommand(string RecordType, Guid RecordId, Guid? LineId, string Dimension) : ICommand;

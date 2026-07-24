namespace FSH.Modules.Platform.Contracts.Dtos;

public sealed record SegmentAssignmentDto(
    Guid Id,
    string Dimension,
    Guid OrgUnitId,
    string OrgUnitCode,
    string OrgUnitName,
    Guid? LineId);

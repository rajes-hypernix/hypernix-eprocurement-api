namespace eProcure.Application.Segments;

/// <summary>Invalid segment definition/value/assignment → HTTP 400, the loud posture.</summary>
public sealed class SegmentValidationException(string message) : Exception(message);

public sealed record SegmentDefDto(
    Guid Id, string Code, string Name, bool HasHierarchy, bool Required, bool Active, bool IsSystem,
    IReadOnlyList<SegmentValueDto> Values, IReadOnlyList<SegmentApplicationDto> Applications);

public sealed record SegmentValueDto(Guid Id, string Code, string Label, Guid? ParentValueId, bool Active, int Sort);

public sealed record SegmentApplicationDto(string RecordType, bool LineLevel);

public sealed record SaveSegmentDefRequest(string Name, bool HasHierarchy, bool Required);

public sealed record SaveSegmentValueRequest(string Label, Guid? ParentValueId, int Sort);

public sealed record ApplySegmentRequest(string RecordType, bool LineLevel);

/// <summary>One segment on one record (or line): def metadata + the assigned value code
/// (null = unassigned — the honest null; the Unassigned bucket in group-by).</summary>
public sealed record SegmentAssignmentDto(
    string SegmentCode, string SegmentName, bool Required,
    IReadOnlyList<SegmentValueDto> Options, string? ValueCode, string? ValueLabel);

public sealed record SaveSegmentAssignmentsRequest(Dictionary<string, string?> Assignments, Guid? LineId);

public interface ISegmentService
{
    Task<IReadOnlyList<SegmentDefDto>> ListDefsAsync(CancellationToken ct = default);
    Task<SegmentDefDto> CreateDefAsync(SaveSegmentDefRequest req, CancellationToken ct = default);
    Task<SegmentDefDto> UpdateDefAsync(Guid id, SaveSegmentDefRequest req, CancellationToken ct = default);
    Task<SegmentDefDto> AddValueAsync(Guid defId, SaveSegmentValueRequest req, CancellationToken ct = default);
    Task<SegmentDefDto> ApplyAsync(Guid defId, ApplySegmentRequest req, CancellationToken ct = default);
    Task<SegmentDefDto> UnapplyAsync(Guid defId, string recordType, CancellationToken ct = default);
    Task<IReadOnlyList<SegmentAssignmentDto>> GetAssignmentsAsync(string recordType, Guid recordId, Guid? lineId, CancellationToken ct = default);
    Task<IReadOnlyList<SegmentAssignmentDto>> SaveAssignmentsAsync(string recordType, Guid recordId, SaveSegmentAssignmentsRequest req, CancellationToken ct = default);
}

/// <summary>The (iii-a) one-way projection: PR dimension COLUMNS (the single truth) →
/// system-segment assignments. Called from BOTH RequisitionService write paths and the
/// dev seeder; the Postgres probe pins that no path forgets it.</summary>
public interface ISegmentProjection
{
    Task ProjectRequisitionAsync(Guid prId, CancellationToken ct = default);
}

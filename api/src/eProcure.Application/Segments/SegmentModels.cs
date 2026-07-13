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

/// <summary>CF2-T6: value-self edit — label/parent/sort/active. The CODE is immutable (it is
/// the dimension key stored in assignments; renaming the label never re-keys history).</summary>
public sealed record UpdateSegmentValueRequest(string Label, Guid? ParentValueId, int Sort, bool Active);

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
    Task<SegmentDefDto> UpdateValueAsync(Guid defId, Guid valueId, UpdateSegmentValueRequest req, CancellationToken ct = default);
    /// <summary>CF2-T6: guarded — a value with assignments DEACTIVATES (never re-keys history);
    /// a clean one hard-deletes. Returns the def either way.</summary>
    Task<SegmentDefDto> DeleteValueAsync(Guid defId, Guid valueId, CancellationToken ct = default);
    Task<SegmentDefDto> SetDefActiveAsync(Guid id, bool active, CancellationToken ct = default);
    /// <summary>CF2-T6: guarded — refused while ANY assignment or value-in-use exists;
    /// otherwise cascades applications + registry rows + values. System defs never.</summary>
    Task DeleteDefAsync(Guid id, CancellationToken ct = default);
    // CF-FIX4-T6: the CF-FIX-3 lifecycle discipline, segment grain — impact report +
    // governed purge (A73) for defs AND values.
    Task<CustomFields.ImpactReportDto> GetReferencesAsync(Guid id, CancellationToken ct = default);
    Task<CustomFields.ImpactReportDto> GetValueReferencesAsync(Guid valueId, CancellationToken ct = default);
    Task PurgeAsync(Guid id, CancellationToken ct = default);
    Task PurgeValueAsync(Guid valueId, CancellationToken ct = default);
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

using eProcure.Domain.Views;

namespace eProcure.Application.Views;

/// <summary>
/// Layers 2+3 of the record-value convention (D5/D6, A2F-T4): the caller must hold the
/// record type's View* action AND the record must be reachable through its EXISTING
/// scoped detail source (vendor scoping, live-invitation and EnsureCanAccess guards all
/// ride along). ONE source — CustomFieldService and SegmentService consumed a 16-line
/// copy-paste twin of this until the audit's NIT-1.
/// </summary>
public interface IRecordReachability
{
    /// <summary>Throws Forbidden when the caller lacks the type's View* action; NotFound
    /// when the record isn't reachable through the caller's scoped source.</summary>
    Task RequireReachableAsync(RecordType type, Guid recordId, CancellationToken ct = default);
}

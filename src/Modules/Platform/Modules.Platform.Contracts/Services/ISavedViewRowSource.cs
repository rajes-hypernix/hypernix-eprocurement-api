namespace FSH.Modules.Platform.Contracts.Services;

/// <summary>
/// A module-owned source of rows for one or more saved-view record types. Platform runs the
/// view (filter/paginate/project) but has no direct query access to other modules' schemas —
/// each owning module registers an implementation that knows how to shape its own entities into
/// PascalCase-keyed rows matching the field registry.
/// </summary>
public interface ISavedViewRowSource
{
    /// <summary>
    /// Returns every row for <paramref name="recordType"/> (pre-filter, pre-page) as
    /// dictionaries keyed by field key, always including an "Id" entry. Throws
    /// <see cref="NotSupportedException"/> for a record type this source does not own, so the
    /// caller can try the next registered source.
    /// </summary>
    Task<IReadOnlyList<IDictionary<string, object?>>> GetRowsAsync(string recordType, CancellationToken ct);
}

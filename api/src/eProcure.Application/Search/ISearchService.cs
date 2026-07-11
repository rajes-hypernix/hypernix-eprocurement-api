namespace eProcure.Application.Search;

/// <summary>One global-search hit: a record the caller may open.</summary>
public sealed record SearchHit(string Type, Guid Id, string Code, string Title);

/// <summary>
/// Global search (D2 navigation shell): read-only code + title matching across
/// Vendor, PR, RFQ, PO and Invoice. Queries only — no schema. Perimeter-scoped:
/// vendor principals see ONLY their own reachable records (own vendor record,
/// live-invitation RFQs, own POs/invoices — the Slice F conventions); PRs are
/// buyer-side records and never reach a vendor principal.
/// </summary>
public interface ISearchService
{
    Task<IReadOnlyList<SearchHit>> SearchAsync(string q, CancellationToken ct = default);
}

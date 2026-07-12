namespace eProcure.Application.Abstractions;

/// <summary>
/// Produces human-readable codes from a server-side sequence per type,
/// e.g. NextAsync("RFQ") -> "RFQ-2026-0001" (BUSINESS-RULES [$]).
/// </summary>
public interface ICodeGenerator
{
    /// <summary>Fixed-format mint for SYSTEM artifacts (GRN, BID, AWD, VU, USR, FORM,
    /// DASH, VIEW) — today's {PREFIX}-{YEAR}-{0:D4}, not admin-configurable (OD-D7-6).</summary>
    Task<string> NextAsync(string prefix, CancellationToken ct = default);

    /// <summary>D7: mint for the seven registry record types — the NumberingScheme is
    /// consulted at FORMAT time only; the gap-free FOR-UPDATE sequence is untouched.</summary>
    Task<string> NextAsync(Domain.Views.RecordType type, CancellationToken ct = default);
}

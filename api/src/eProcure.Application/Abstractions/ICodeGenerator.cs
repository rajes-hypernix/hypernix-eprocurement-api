namespace eProcure.Application.Abstractions;

/// <summary>
/// Produces human-readable codes from a server-side sequence per type,
/// e.g. NextAsync("RFQ") -> "RFQ-2026-0001" (BUSINESS-RULES [$]).
/// </summary>
public interface ICodeGenerator
{
    Task<string> NextAsync(string prefix, CancellationToken ct = default);
}

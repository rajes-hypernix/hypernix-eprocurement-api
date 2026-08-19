using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Sourcing.Domain;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs;

/// <summary>
/// Resolves RFQ Incoterm selection against Platform master rows (id preferred; code legacy).
/// Pure helper so unit tests cover the rules without a DbContext.
/// </summary>
internal static class RfqIncotermSupport
{
    internal static (Guid? Id, string? Code) Resolve(
        Guid? incotermId,
        string? incotermCode,
        IReadOnlyList<IncotermDto> all)
    {
        ArgumentNullException.ThrowIfNull(all);

        if (incotermId is null && string.IsNullOrWhiteSpace(incotermCode))
            return (null, null);

        if (incotermId is Guid id)
        {
            var hit = all.FirstOrDefault(x => x.Id == id)
                ?? throw new SourcingRuleException($"Incoterm {id} was not found.");
            if (!hit.IsActive)
                throw new SourcingRuleException($"Incoterm '{hit.Code}' is inactive.");
            return (hit.Id, hit.Code);
        }

        var code = incotermCode!.Trim().ToUpperInvariant();
        var byCode = all.FirstOrDefault(x =>
            string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase) && x.IsActive);
        if (byCode is null)
            throw new SourcingRuleException($"Incoterm code '{code}' is not an active master term.");
        return (byCode.Id, byCode.Code);
    }
}

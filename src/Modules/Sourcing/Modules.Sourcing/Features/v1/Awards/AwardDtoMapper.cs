using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Domain;

namespace FSH.Modules.Sourcing.Features.v1.Awards;

internal static class AwardDtoMapper
{
    internal static AwardDto ToDto(Award award)
    {
        ArgumentNullException.ThrowIfNull(award);
        return new AwardDto(
            award.Id,
            award.Code,
            award.RfqId,
            award.Status.ToString(),
            award.CreatedByUserId,
            award.ApproverUserId,
            award.ApprovedUtc,
            award.TotalValue,
            [.. award.Allocations.Select(a => new AwardAllocationDto(a.RfqLineCode, a.VendorId, a.Qty, a.UnitPrice))],
            award.CreatedUtc,
            award.UpdatedUtc);
    }
}

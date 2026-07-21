using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Domain;

namespace FSH.Modules.Sourcing.Features.v1.Clarifications;

internal static class ClarificationDtoMapper
{
    internal static ClarificationMessageDto ToDto(Clarification c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return new ClarificationMessageDto(c.Id, c.Scope, c.VendorId, c.SenderKind.ToString(), c.SenderName, c.Body, c.Published, c.CreatedUtc, c.ReadByBuyer, c.ReadByVendor);
    }
}

using FSH.Modules.Sourcing.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Rfqs;

public sealed record InviteVendorCommand(Guid RfqId, Guid VendorId) : ICommand<RfqInvitationDto>;

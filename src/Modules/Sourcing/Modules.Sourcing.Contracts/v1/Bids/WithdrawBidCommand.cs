using FSH.Modules.Sourcing.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Bids;

public sealed record WithdrawBidCommand(Guid RfqId) : ICommand<BidDto>;

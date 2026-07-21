using FSH.Modules.Sourcing.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Clarifications;

/// <summary>
/// A vendor sender always targets their own thread (VendorId ignored — derived from the caller's
/// vendorId claim). A buyer sender either targets one specific vendor's thread (VendorId set,
/// Published false) or publishes to every live invited vendor on the RFQ named by
/// <paramref name="Scope"/> (VendorId null, Published true, Scope != "general").
/// </summary>
public sealed record SendClarificationCommand(string Scope, Guid? VendorId, string Body, bool Published)
    : ICommand<IReadOnlyList<ClarificationMessageDto>>;

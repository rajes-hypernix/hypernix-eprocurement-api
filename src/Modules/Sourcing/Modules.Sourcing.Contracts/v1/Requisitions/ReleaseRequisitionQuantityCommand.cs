using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Requisitions;

/// <summary>
/// Internal, cross-module compensating command — releases every active <c>PrLineOrder</c>
/// reservation tied to the given PO (e.g. the PO's own Cancel, or a failed PO save rolling back a
/// prior reserve). No-op if nothing is reserved for that PO. Called via <see cref="IMediator"/>
/// from Procurement.
/// </summary>
public sealed record ReleaseRequisitionQuantityCommand(Guid PoId, string? Reason) : ICommand;

using FSH.Modules.Sourcing.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Requisitions;

/// <summary>
/// Requisition lines eligible for the direct-order builder workspace — mirrors the old source's
/// PoBuilderService.EligibleLinesAsync/Classify exactly: PR headers Submitted or PartiallySourced,
/// classified per line by <c>PrLineStatus</c>. Eligibility is a status filter, not a pre-assigned
/// vendor/price — vendor+price get assigned by the buyer in the order-builder screen itself.
/// </summary>
public sealed record GetEligibleLinesForOrderingQuery : IQuery<IReadOnlyList<EligibleOrderLineDto>>;

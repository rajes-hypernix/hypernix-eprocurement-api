using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Requisitions;

public sealed record UpdateRequisitionCommand(
    Guid RequisitionId,
    string Department,
    string? DepartmentCode,
    string Location,
    string? LocationCode,
    string Category,
    string? CategoryCode,
    string Job,
    string? JobCode,
    string Memo,
    string CostCentre,
    string? Project,
    DateOnly? RequiredOn) : ICommand<Guid>;

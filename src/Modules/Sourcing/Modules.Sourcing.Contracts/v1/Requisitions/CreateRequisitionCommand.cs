using FSH.Modules.Sourcing.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Requisitions;

public sealed record CreateRequisitionCommand(
    string Requestor,
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
    Guid? EntryFormId,
    DateOnly? RaisedOn,
    DateOnly? RequiredOn,
    string Currency,
    IReadOnlyList<PrLineInput> Lines,
    bool Submit = false,
    Guid? ShipToLocationId = null,
    Guid? ShipToAddressId = null,
    string? ShipToAdhoc = null) : ICommand<Guid>;

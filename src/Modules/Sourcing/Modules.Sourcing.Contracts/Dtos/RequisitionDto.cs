namespace FSH.Modules.Sourcing.Contracts.Dtos;

public sealed record RequisitionDto(
    Guid Id,
    string Code,
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
    bool Submitted,
    DateTime? SubmittedUtc,
    string HeaderStatus,
    string Currency,
    IReadOnlyList<PrLineDto> Lines,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);

public sealed record RequisitionListItemDto(
    Guid Id,
    string Code,
    string Requestor,
    string Department,
    string HeaderStatus,
    bool Submitted,
    int LineCount,
    DateTime CreatedUtc);

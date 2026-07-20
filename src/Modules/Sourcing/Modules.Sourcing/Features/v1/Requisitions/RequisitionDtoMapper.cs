using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Domain;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions;

internal static class RequisitionDtoMapper
{
    internal static RequisitionDto ToDto(PurchaseRequisition pr)
    {
        ArgumentNullException.ThrowIfNull(pr);
        return new RequisitionDto(
            pr.Id,
            pr.Code,
            pr.Requestor,
            pr.Department,
            pr.DepartmentCode,
            pr.Location,
            pr.LocationCode,
            pr.Category,
            pr.CategoryCode,
            pr.Job,
            pr.JobCode,
            pr.Memo,
            pr.CostCentre,
            pr.Project,
            pr.EntryFormId,
            pr.RaisedOn,
            pr.RequiredOn,
            pr.Submitted,
            pr.SubmittedUtc,
            pr.HeaderStatus.ToString(),
            pr.Currency,
            [.. pr.Lines.Select(ToLineDto)],
            pr.CreatedUtc,
            pr.UpdatedUtc);
    }

    internal static PrLineDto ToLineDto(PrLine line) =>
        new(line.Id, line.ItemCode, line.Description, line.Qty, line.Uom, line.EstUnitPrice, line.LifecycleStatus.ToString(), line.Ref);

    internal static RequisitionListItemDto ToListItemDto(PurchaseRequisition pr) =>
        new(pr.Id, pr.Code, pr.Requestor, pr.Department, pr.HeaderStatus.ToString(), pr.Submitted, pr.Lines.Count, pr.CreatedUtc);
}

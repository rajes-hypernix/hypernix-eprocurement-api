using FSH.Framework.Core.Common;
using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Domain;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions;

internal static class RequisitionDtoMapper
{
    internal static RequisitionDto ToDto(
        PurchaseRequisition pr,
        IReadOnlyDictionary<Guid, TaxCodeDto>? taxes = null,
        string? shipTo = null)
    {
        ArgumentNullException.ThrowIfNull(pr);
        taxes ??= new Dictionary<Guid, TaxCodeDto>();
        var lines = pr.Lines
            .OrderBy(l => l.LineSequence)
            .ThenBy(l => l.Id)
            .Select(l => ToLineDto(l, taxes))
            .ToList();

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
            lines,
            pr.CreatedUtc,
            pr.UpdatedUtc,
            pr.ShipToLocationId,
            pr.ShipToAddressId,
            pr.ShipToAdhoc,
            shipTo ?? (string.IsNullOrWhiteSpace(pr.ShipToAdhoc) ? null : pr.ShipToAdhoc));
    }

    internal static PrLineDto ToLineDto(PrLine line, IReadOnlyDictionary<Guid, TaxCodeDto> taxes)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(taxes);

        var estAmount = line.Qty * line.EstUnitPrice;
        string? taxCode = null;
        decimal? ratePct = null;
        decimal sst = 0m;
        if (line.TaxCodeId is { } taxId && taxes.TryGetValue(taxId, out var tax))
        {
            taxCode = tax.Code;
            ratePct = tax.RatePct;
            sst = TaxMath.LineSst(estAmount, tax.RatePct);
        }

        return new PrLineDto(
            line.Id,
            line.ItemCode,
            line.Description,
            line.Qty,
            line.Uom,
            line.EstUnitPrice,
            line.LifecycleStatus.ToString(),
            line.Ref,
            line.LineSequence,
            line.TaxCodeId,
            taxCode,
            ratePct,
            sst,
            estAmount);
    }

    internal static RequisitionListItemDto ToListItemDto(PurchaseRequisition pr) =>
        new(pr.Id, pr.Code, pr.Requestor, pr.Department, pr.HeaderStatus.ToString(), pr.Submitted, pr.Lines.Count, pr.CreatedUtc);
}

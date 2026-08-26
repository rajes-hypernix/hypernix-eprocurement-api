using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Domain;

namespace FSH.Modules.Procurement.Features.v1;

internal static class ProcurementDtoMapper
{
    internal static PurchaseOrderDto ToDto(PurchaseOrder po, string? vendorName = null) => new(
        po.Id, po.Code, po.AwardId, po.RfqId, po.SourcePrId, po.SourceKind.ToString(), po.VendorId,
        po.Status.ToString(), po.Currency,
        po.ShipToLocationId, po.ShipToAddressId, po.ShipToAdhoc,
        po.IncotermId, po.IncotermCode, po.IncotermSuffix, po.Memo, po.VendorRef, po.RequiredDate, po.DeliveryDate,
        po.VerifiedUtc, po.IssuedUtc, po.EntryFormId, po.TotalValue,
        [.. po.Lines.Select(ToDto)],
        po.CreatedUtc, po.UpdatedUtc,
        vendorName ?? "");

    internal static PoLineDto ToDto(PoLine l) => new(
        l.Id, l.ItemCode, l.Description, l.Uom, l.Qty, l.UnitPrice,
        l.ReceivedQty, l.InvoicedQty, l.RfqLineCode, l.SourcePrLineId, l.TaxCodeId, l.PriceConfirmed, l.LineTotal,
        Math.Max(0, l.Qty - l.ReceivedQty));

    internal static AsnDto ToDto(Asn asn, string? poCode = null, string? vendorName = null, PurchaseOrder? po = null) => new(
        asn.Id, asn.Code, asn.PoId, asn.Status.ToString(),
        asn.Carrier, asn.TrackingNo, asn.ShippedDate, asn.ExpectedDate,
        [.. asn.Lines.Select(l => ToDto(l, po))],
        asn.CreatedUtc,
        poCode ?? po?.Code,
        vendorName);

    internal static AsnLineDto ToDto(AsnLine l, PurchaseOrder? po = null)
    {
        var poLine = po?.Lines.FirstOrDefault(x => string.Equals(x.ItemCode, l.ItemCode, StringComparison.Ordinal));
        return new(l.Id, l.ItemCode, l.ShippedQty, l.LotNo, poLine?.Description, poLine?.Uom);
    }

    internal static GrnDto ToDto(Grn grn) => new(
        grn.Id, grn.Code, grn.AsnId, grn.PoId,
        [.. grn.Lines.Select(ToDto)],
        grn.CreatedUtc);

    internal static GrnLineDto ToDto(GrnLine l) => new(l.Id, l.ItemCode, l.ExpectedQty, l.ReceivedQty, l.Condition);

    internal static InvoiceDto ToDto(Invoice inv) => new(
        inv.Id, inv.Code, inv.PoId, inv.GrnId, inv.InvoiceNo,
        inv.Status.ToString(), inv.InvoiceDate,
        inv.Subtotal, inv.SstAmount, inv.WhtAmount, inv.Total,
        Domain.Invoice.SstRate, inv.WhtRate, inv.ExceptionReason,
        [.. inv.Lines.Select(ToDto)],
        inv.CreatedUtc);

    internal static InvoiceLineDto ToDto(InvoiceLine l) => new(l.Id, l.ItemCode, l.Qty, l.UnitPrice, l.LineTotal);

    internal static PurchaseOrderListItemDto ToListItem(PurchaseOrder po, string? vendorName = null) => new(
        po.Id, po.Code, po.RfqId, po.SourcePrId, po.SourceKind.ToString(), po.VendorId,
        po.Status.ToString(), po.Currency, po.TotalValue, po.CreatedUtc,
        vendorName ?? "",
        po.Lines.Sum(l => l.ReceivedQty),
        po.Lines.Sum(l => l.Qty),
        po.AwardId);
}

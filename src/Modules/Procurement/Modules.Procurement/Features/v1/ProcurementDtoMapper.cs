using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Domain;

namespace FSH.Modules.Procurement.Features.v1;

internal static class ProcurementDtoMapper
{
    internal static PurchaseOrderDto ToDto(PurchaseOrder po) => new(
        po.Id, po.Code, po.AwardId, po.RfqId, po.VendorId,
        po.Status.ToString(), po.Currency, po.TotalValue,
        [.. po.Lines.Select(ToDto)],
        po.CreatedUtc, po.UpdatedUtc);

    internal static PoLineDto ToDto(PoLine l) => new(
        l.Id, l.ItemCode, l.Description, l.Uom, l.Qty, l.UnitPrice,
        l.ReceivedQty, l.InvoicedQty, l.RfqLineCode);

    internal static AsnDto ToDto(Asn asn) => new(
        asn.Id, asn.Code, asn.PoId, asn.Status.ToString(),
        asn.Carrier, asn.TrackingNo, asn.ShippedDate, asn.ExpectedDate,
        [.. asn.Lines.Select(ToDto)],
        asn.CreatedUtc);

    internal static AsnLineDto ToDto(AsnLine l) => new(l.Id, l.ItemCode, l.ShippedQty, l.LotNo);

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

    internal static PurchaseOrderListItemDto ToListItem(PurchaseOrder po) => new(
        po.Id, po.Code, po.RfqId, po.VendorId,
        po.Status.ToString(), po.Currency, po.TotalValue, po.CreatedUtc);
}

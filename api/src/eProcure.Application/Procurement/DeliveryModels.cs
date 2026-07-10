namespace eProcure.Application.Procurement;

public sealed record ShipPlanLineDto(
    string ItemCode, string Description, decimal Ordered, decimal Received, decimal InTransit, decimal Remaining, string Uom);

public sealed record ShipPlanDto(
    Guid PoId, string PoCode, bool AnyRemaining, IReadOnlyList<ShipPlanLineDto> Lines);

public sealed record AsnLineDto(string ItemCode, string Description, decimal ShippedQty, string Uom, string LotNo);
public sealed record AsnListDto(Guid Id, string Code, string PoCode, string VendorName, string Carrier, string ExpectedDate, string Status, string? GrnCode);
public sealed record AsnDetailDto(
    Guid Id, string Code, Guid PoId, string PoCode, string VendorName, string Carrier, string TrackingNo,
    string ShippedDate, string ExpectedDate, string Status, string? GrnCode, IReadOnlyList<AsnLineDto> Lines);

public sealed record CreateAsnLine(string ItemCode, decimal ShippedQty, string LotNo);
public sealed record CreateAsnRequest(string Carrier, string TrackingNo, string ShippedDate, string ExpectedDate, IReadOnlyList<CreateAsnLine> Lines);

public sealed record GrnLineDto(string ItemCode, string Description, decimal ExpectedQty, decimal ReceivedQty, string Condition);
public sealed record GrnDetailDto(Guid Id, string Code, Guid AsnId, string AsnCode, string PoCode, string ReceivedDate, string? NsId, IReadOnlyList<GrnLineDto> Lines);

public sealed record ReceiveLine(string ItemCode, decimal ReceivedQty, string Condition);
public sealed record ReceiveRequest(IReadOnlyList<ReceiveLine> Lines);

public interface IDeliveryService
{
    Task<IReadOnlyList<AsnListDto>> ListAsync(CancellationToken ct = default);
    Task<AsnDetailDto?> GetAsync(Guid asnId, CancellationToken ct = default);
    Task<ShipPlanDto> GetShipPlanAsync(Guid poId, CancellationToken ct = default);
    Task<AsnDetailDto> CreateAsnAsync(Guid poId, CreateAsnRequest req, CancellationToken ct = default);
    Task<GrnDetailDto?> GetGrnForAsnAsync(Guid asnId, CancellationToken ct = default);
    Task<GrnDetailDto> ReceiveAsync(Guid asnId, ReceiveRequest req, CancellationToken ct = default);
}

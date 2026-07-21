namespace FSH.Modules.Procurement.Data;

public interface IProcurementCodeGenerator
{
    Task<string> NextPoCodeAsync(CancellationToken cancellationToken);
    Task<string> NextAsnCodeAsync(CancellationToken cancellationToken);
    Task<string> NextGrnCodeAsync(CancellationToken cancellationToken);
    Task<string> NextInvoiceCodeAsync(CancellationToken cancellationToken);
}

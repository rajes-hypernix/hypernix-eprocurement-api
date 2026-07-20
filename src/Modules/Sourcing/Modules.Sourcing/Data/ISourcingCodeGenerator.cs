namespace FSH.Modules.Sourcing.Data;

public interface ISourcingCodeGenerator
{
    Task<string> NextPurchaseRequisitionCodeAsync(CancellationToken cancellationToken);

    Task<string> NextRfqCodeAsync(CancellationToken cancellationToken);
}

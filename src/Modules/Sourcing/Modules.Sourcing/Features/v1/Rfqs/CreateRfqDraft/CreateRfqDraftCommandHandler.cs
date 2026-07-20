using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using Mediator;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.CreateRfqDraft;

public sealed class CreateRfqDraftCommandHandler(SourcingDbContext dbContext, ISourcingCodeGenerator codeGenerator)
    : ICommandHandler<CreateRfqDraftCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateRfqDraftCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        string code = await codeGenerator.NextRfqCodeAsync(cancellationToken).ConfigureAwait(false);
        var envelope = Enum.Parse<RfqEnvelope>(command.Envelope);
        var lines = command.Lines.Select(l => new RfqLine(l.LineCode, l.ItemCode, l.Description, l.Qty, l.Uom, l.PrRef, l.SourcePrLineIds?.ToList()));

        var rfq = Rfq.CreateDraft(code, command.Title, envelope, command.Currency, ownerUserId: null, command.PrRefs, [.. lines]);

        dbContext.Rfqs.Add(rfq);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rfq.Id;
    }
}

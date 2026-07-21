using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.UpdateRfqDraft;

public sealed class UpdateRfqDraftCommandHandler(SourcingDbContext dbContext)
    : ICommandHandler<UpdateRfqDraftCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdateRfqDraftCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var rfq = await dbContext.Rfqs
            .FirstOrDefaultAsync(r => r.Id == command.RfqId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"RFQ {command.RfqId} not found.");

        var envelope = Enum.Parse<RfqEnvelope>(command.Envelope);
        var lines = command.Lines.Select(l => new RfqLine(l.LineCode, l.ItemCode, l.Description, l.Qty, l.Uom, l.PrRef, l.SourcePrLineIds?.ToList()));
        var formItems = command.FormItems.Select(f => new FormItem(f.Kind, f.Group, f.Section, f.Label, f.Type, f.Required, f.ConfigJson, f.Help, f.Order));

        rfq.UpdateDraft(
            command.Title, envelope, command.Currency, command.OpensUtc, command.ClosesUtc, [.. lines], [.. formItems],
            command.TechnicalSections, command.CommercialSections, command.TechnicalEvaluatorIds, command.CommercialEvaluatorIds);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rfq.Id;
    }
}

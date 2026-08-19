using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.UpdateRfqDraft;

public sealed class UpdateRfqDraftCommandHandler(SourcingDbContext dbContext, IMediator mediator)
    : ICommandHandler<UpdateRfqDraftCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdateRfqDraftCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var rfq = await dbContext.Rfqs
            .FirstOrDefaultAsync(r => r.Id == command.RfqId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"RFQ {command.RfqId} not found.");

        var all = await mediator.Send(new ListIncotermsQuery(ActiveOnly: false), cancellationToken).ConfigureAwait(false);
        var (incotermId, incotermCode) = RfqIncotermSupport.Resolve(command.IncotermId, command.IncotermCode, all);

        var previousCurrency = rfq.Currency;
        var envelope = Enum.Parse<RfqEnvelope>(command.Envelope);
        var lines = command.Lines.Select(l => new RfqLine(l.LineCode, l.ItemCode, l.Description, l.Qty, l.Uom, l.PrRef, l.SourcePrLineIds?.ToList()));
        var formItems = command.FormItems.Select(f => new FormItem(f.Kind, f.Group, f.Section, f.Label, f.Type, f.Required, f.ConfigJson, f.Help, f.Order));

        rfq.UpdateDraft(
            command.Title, envelope, command.Currency, command.OpensUtc, command.ClosesUtc, [.. lines], [.. formItems],
            command.TechnicalSections, command.CommercialSections, command.TechnicalEvaluatorIds, command.CommercialEvaluatorIds,
            command.ClarificationDeadlineUtc, command.BidValidityDays, command.PartialBidsAllowed,
            incotermId, incotermCode, command.IncotermSuffix);

        // Currency change → re-snapshot (POC leaves stale until Update-rate; we refresh so the row matches).
        if (!string.Equals(previousCurrency, rfq.Currency, StringComparison.OrdinalIgnoreCase))
        {
            var rates = await mediator.Send(new ListCurrentExchangeRatesQuery(), cancellationToken).ConfigureAwait(false);
            rfq.SetExchangeRateToBase(RfqExchangeRateSupport.ResolveRateToBase(rfq.Currency, rates));
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rfq.Id;
    }
}

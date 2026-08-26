using FSH.Framework.Eventing.Abstractions;
using FSH.Modules.Communication.Contracts.Events;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using Mediator;
using Microsoft.Extensions.Logging;

namespace FSH.Modules.Procurement.IntegrationEventHandlers;

/// <summary>POC parity: approving an award generates one Draft PO per awarded vendor.</summary>
public sealed class AwardApprovedCreatePurchaseOrdersHandler(
    IMediator mediator,
    ILogger<AwardApprovedCreatePurchaseOrdersHandler> logger)
    : IIntegrationEventHandler<AwardApprovedIntegrationEvent>
{
    public async Task HandleAsync(AwardApprovedIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var ids = await mediator.Send(new CreatePurchaseOrdersFromAwardCommand(@event.RfqId), ct).ConfigureAwait(false);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Award {AwardCode} generated {Count} purchase order(s)", @event.AwardCode, ids.Count);
        }
    }
}

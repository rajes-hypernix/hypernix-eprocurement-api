using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Domain;
using Mediator;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions;

internal static class RequisitionWriteSupport
{
    internal static async Task EnsureActiveTaxCodesAsync(
        IMediator mediator,
        IEnumerable<PrLineInput> lines,
        CancellationToken cancellationToken)
    {
        var ids = lines.Where(l => l.TaxCodeId is not null).Select(l => l.TaxCodeId!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var taxes = await mediator.Send(new ListTaxCodesQuery(ActiveOnly: false), cancellationToken).ConfigureAwait(false);
        var byId = taxes.ToDictionary(t => t.Id);
        foreach (var id in ids)
        {
            if (!byId.TryGetValue(id, out var tax) || !tax.IsActive)
            {
                throw new SourcingRuleException($"Tax code {id} is not an active tax code.");
            }
        }
    }

    internal static async Task EnsureShipToLocationAsync(
        IMediator mediator,
        Guid? locationId,
        Guid? addressId,
        string? adhoc,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(adhoc) || (locationId is null && addressId is null))
        {
            return;
        }

        if (locationId is not { } locId || addressId is not { } addrId)
        {
            throw new SourcingRuleException("A location ship-to needs both a location and an address.");
        }

        LocationDto location;
        try
        {
            location = await mediator.Send(new GetLocationQuery(locId), cancellationToken).ConfigureAwait(false);
        }
        catch (NotFoundException)
        {
            throw new NotFoundException($"Location {locId} not found.");
        }

        if (!location.IsActive)
        {
            throw new NotFoundException($"Location {locId} not found or inactive.");
        }

        if (location.Addresses.All(a => a.Id != addrId))
        {
            throw new NotFoundException($"Address {addrId} is not on location {location.Code}.");
        }
    }

    internal static async Task<IReadOnlyDictionary<Guid, TaxCodeDto>> LoadTaxesAsync(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var taxes = await mediator.Send(new ListTaxCodesQuery(ActiveOnly: false), cancellationToken).ConfigureAwait(false);
        return taxes.ToDictionary(t => t.Id);
    }

    internal static async Task<string?> ResolveShipToAsync(
        IMediator mediator,
        PurchaseRequisition pr,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(pr.ShipToAdhoc))
        {
            return pr.ShipToAdhoc;
        }

        if (pr.ShipToLocationId is not { } locId || pr.ShipToAddressId is not { } addrId)
        {
            return null;
        }

        try
        {
            var location = await mediator.Send(new GetLocationQuery(locId), cancellationToken).ConfigureAwait(false);
            var address = location.Addresses.FirstOrDefault(a => a.Id == addrId);
            if (address is null)
            {
                return null;
            }

            var cityLine = string.Join(", ",
                new[] { address.City, $"{address.State} {address.Postcode}".Trim() }.Where(x => !string.IsNullOrWhiteSpace(x)));
            return string.Join(" · ",
                new[] { address.Label, address.Line1, address.Line2, cityLine, address.Country }
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
        }
        catch (NotFoundException)
        {
            return null;
        }
    }
}

using FSH.Framework.Core.Context;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.ExchangeRates.AppendExchangeRate;

public sealed class AppendExchangeRateCommandHandler(PlatformDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<AppendExchangeRateCommand, Guid>
{
    public async ValueTask<Guid> Handle(AppendExchangeRateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var code = command.CurrencyCode.Trim().ToUpperInvariant();

        var currency = await dbContext.Currencies
            .FirstOrDefaultAsync(c => c.Code == code, cancellationToken)
            .ConfigureAwait(false);
        if (currency is null || !currency.IsActive)
            throw new PlatformRuleException($"Currency '{code}' is missing or inactive.");

        var baseCurrency = await dbContext.Settings.AsNoTracking()
            .Where(s => s.Key == SettingKeys.BaseCurrency)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(baseCurrency) && string.Equals(baseCurrency, code, StringComparison.OrdinalIgnoreCase))
            throw new PlatformRuleException($"Cannot append an exchange rate for the base currency '{code}'.");

        var enteredBy = currentUser.GetUserId() is { } userId && userId != Guid.Empty
            ? userId.ToString()
            : "system";

        var rate = ExchangeRate.Create(code, command.RateToBase, command.EffectiveDate, enteredBy);
        dbContext.ExchangeRates.Add(rate);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rate.Id;
    }
}

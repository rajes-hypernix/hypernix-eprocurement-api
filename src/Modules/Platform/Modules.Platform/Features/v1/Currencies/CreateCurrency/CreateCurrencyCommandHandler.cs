using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Currencies.CreateCurrency;

public sealed class CreateCurrencyCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<CreateCurrencyCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateCurrencyCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var code = command.Code.Trim().ToUpperInvariant();
        bool exists = await dbContext.Currencies
            .AnyAsync(c => c.Code == code, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
            throw new PlatformRuleException($"Currency '{code}' already exists.");

        var currency = Currency.Create(command.Code, command.Name, command.Symbol, command.Decimals);
        dbContext.Currencies.Add(currency);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return currency.Id;
    }
}

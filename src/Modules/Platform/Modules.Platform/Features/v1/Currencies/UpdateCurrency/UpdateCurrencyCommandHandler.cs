using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Currencies.UpdateCurrency;

public sealed class UpdateCurrencyCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<UpdateCurrencyCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdateCurrencyCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var currency = await dbContext.Currencies
            .FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Currency {command.Id} not found.");

        currency.Update(command.Name, command.Symbol, command.Decimals);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return currency.Id;
    }
}

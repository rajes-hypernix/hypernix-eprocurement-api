using FSH.Modules.Platform.Contracts.v1.Banks;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Banks.CreateBank;

public sealed class CreateBankCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<CreateBankCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateBankCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var country = command.CountryCode.Trim().ToUpperInvariant();
        var name = command.Name.Trim();
        bool exists = await dbContext.Banks
            .AnyAsync(b => b.CountryCode == country && b.Name == name, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
            throw new PlatformRuleException($"Bank '{name}' already exists for {country}.");

        var bank = Bank.Create(command.Name, command.CountryCode, command.SwiftCode);
        dbContext.Banks.Add(bank);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return bank.Id;
    }
}

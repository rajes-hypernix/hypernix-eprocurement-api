using FSH.Modules.Platform.Contracts.v1.Banks;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Banks.UpdateBank;

public sealed class UpdateBankCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<UpdateBankCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdateBankCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var bank = await dbContext.Banks
            .FirstOrDefaultAsync(b => b.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new PlatformRuleException($"Bank '{command.Id}' was not found.");

        var country = command.CountryCode.Trim().ToUpperInvariant();
        var name = command.Name.Trim();
        bool duplicate = await dbContext.Banks
            .AnyAsync(
                b => b.Id != command.Id && b.CountryCode == country && b.Name == name,
                cancellationToken)
            .ConfigureAwait(false);
        if (duplicate)
            throw new PlatformRuleException($"Bank '{name}' already exists for {country}.");

        bank.Update(command.Name, command.CountryCode, command.SwiftCode);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return bank.Id;
    }
}

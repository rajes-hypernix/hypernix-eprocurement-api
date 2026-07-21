using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.Banks;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Banks.SetBankActive;

public sealed class SetBankActiveCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<SetBankActiveCommand, Guid>
{
    public async ValueTask<Guid> Handle(SetBankActiveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var bank = await dbContext.Banks.FirstOrDefaultAsync(b => b.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Bank {command.Id} not found.");

        bank.SetActive(command.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return bank.Id;
    }
}

using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Settings.UpdateSetting;

public sealed class UpdateSettingCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<UpdateSettingCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdateSettingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var key = command.Key.Trim();
        var setting = await dbContext.Settings
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Setting '{key}' was not found.");

        setting.SetValue(command.Value);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return setting.Id;
    }
}

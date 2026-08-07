using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Identity.Contracts.v1.Users.AdminSetPassword;
using Mediator;

namespace FSH.Modules.Identity.Features.v1.Users.AdminSetPassword;

public sealed class AdminSetPasswordCommandHandler(IUserPasswordService passwordService)
    : ICommandHandler<AdminSetPasswordCommand, string>
{
    public async ValueTask<string> Handle(AdminSetPasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await passwordService.AdminSetPasswordAsync(
            command.UserId,
            command.Password,
            command.ConfirmPassword,
            cancellationToken).ConfigureAwait(false);
        return "Password updated.";
    }
}

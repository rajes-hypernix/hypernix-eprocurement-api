using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Identity.Contracts.v1.Users.AdminUpdateUser;
using Mediator;

namespace FSH.Modules.Identity.Features.v1.Users.AdminUpdateUser;

public sealed class AdminUpdateUserCommandHandler(IUserProfileService profileService)
    : ICommandHandler<AdminUpdateUserCommand, Unit>
{
    public async ValueTask<Unit> Handle(AdminUpdateUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await profileService.AdminUpdateAsync(
            command.UserId,
            command.FirstName,
            command.LastName,
            command.PhoneNumber,
            command.Email,
            cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

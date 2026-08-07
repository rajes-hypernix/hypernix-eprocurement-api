using Mediator;

namespace FSH.Modules.Identity.Contracts.v1.Users.AdminSetPassword;

public sealed class AdminSetPasswordCommand : ICommand<string>
{
    public string UserId { get; set; } = default!;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

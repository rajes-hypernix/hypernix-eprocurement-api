using Mediator;

namespace FSH.Modules.Identity.Contracts.v1.Users.AdminUpdateUser;

public sealed class AdminUpdateUserCommand : ICommand<Unit>
{
    public string UserId { get; set; } = default!;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
}

using FluentValidation;
using FSH.Modules.Identity.Contracts.v1.Users.AdminSetPassword;

namespace FSH.Modules.Identity.Features.v1.Users.AdminSetPassword;

public sealed class AdminSetPasswordCommandValidator : AbstractValidator<AdminSetPasswordCommand>
{
    public AdminSetPasswordCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password)
            .WithMessage("Passwords do not match.");
    }
}

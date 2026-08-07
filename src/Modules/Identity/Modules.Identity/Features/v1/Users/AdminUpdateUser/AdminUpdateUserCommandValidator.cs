using FluentValidation;
using FSH.Modules.Identity.Contracts.v1.Users.AdminUpdateUser;

namespace FSH.Modules.Identity.Features.v1.Users.AdminUpdateUser;

public sealed class AdminUpdateUserCommandValidator : AbstractValidator<AdminUpdateUserCommand>
{
    public AdminUpdateUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(75);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(75);
        RuleFor(x => x.PhoneNumber).MaximumLength(20).When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

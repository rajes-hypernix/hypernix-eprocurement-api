using FluentValidation;
using FSH.Modules.Identity.Contracts.v1.ResolveTenant;

namespace FSH.Modules.Identity.Features.v1.ResolveTenant;

public sealed class ResolveTenantByEmailQueryValidator : AbstractValidator<ResolveTenantByEmailQuery>
{
    public ResolveTenantByEmailQueryValidator()
    {
        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .EmailAddress();
    }
}

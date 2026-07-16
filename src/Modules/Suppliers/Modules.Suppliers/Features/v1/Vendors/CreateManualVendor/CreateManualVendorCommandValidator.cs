using FluentValidation;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;

namespace FSH.Modules.Suppliers.Features.v1.Vendors.CreateManualVendor;

public sealed class CreateManualVendorCommandValidator : AbstractValidator<CreateManualVendorCommand>
{
    public CreateManualVendorCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RegistrationNo).NotNull();
        RuleFor(x => x.Type).NotEmpty();
    }
}

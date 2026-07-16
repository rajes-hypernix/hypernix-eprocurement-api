using FluentValidation;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;

namespace FSH.Modules.Suppliers.Features.v1.Vendors.CreateVendor;

public sealed class CreateVendorCommandValidator : AbstractValidator<CreateVendorCommand>
{
    public CreateVendorCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

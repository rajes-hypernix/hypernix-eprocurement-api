using FluentValidation;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;

namespace FSH.Modules.Suppliers.Features.v1.Vendors.UpdateVendor;

public sealed class UpdateVendorCommandValidator : AbstractValidator<UpdateVendorCommand>
{
    public UpdateVendorCommandValidator()
    {
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RegisteredName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RegistrationNo).NotEmpty().MaximumLength(60);
        RuleFor(x => x.TaxId).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Type).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Region).NotEmpty().MaximumLength(50);
        RuleFor(x => x.State).NotEmpty().MaximumLength(100);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PaymentTerms).NotEmpty().MaximumLength(50);
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Rating).InclusiveBetween(0, 5);
    }
}

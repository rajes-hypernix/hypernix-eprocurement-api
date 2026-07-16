using FluentValidation;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;

namespace FSH.Modules.Suppliers.Features.v1.Vendors.SetVendorCategories;

public sealed class SetVendorCategoriesCommandValidator : AbstractValidator<SetVendorCategoriesCommand>
{
    public SetVendorCategoriesCommandValidator()
    {
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.Categories).NotNull();
    }
}

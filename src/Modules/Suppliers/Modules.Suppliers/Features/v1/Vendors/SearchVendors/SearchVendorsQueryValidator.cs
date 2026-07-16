using FluentValidation;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;

namespace FSH.Modules.Suppliers.Features.v1.Vendors.SearchVendors;

public sealed class SearchVendorsQueryValidator : AbstractValidator<SearchVendorsQuery>
{
    public SearchVendorsQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

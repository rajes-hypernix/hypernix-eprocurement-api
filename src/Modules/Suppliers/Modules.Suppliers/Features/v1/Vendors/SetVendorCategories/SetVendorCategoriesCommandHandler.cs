using FSH.Framework.Core.Exceptions;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;
using FSH.Modules.Suppliers.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Features.v1.Vendors.SetVendorCategories;

public sealed class SetVendorCategoriesCommandHandler(SuppliersDbContext dbContext)
    : ICommandHandler<SetVendorCategoriesCommand, Guid>
{
    public async ValueTask<Guid> Handle(SetVendorCategoriesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vendor = await dbContext.Vendors
            .FirstOrDefaultAsync(v => v.Id == command.VendorId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Vendor {command.VendorId} not found.");

        vendor.SetCategories(command.Categories);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return vendor.Id;
    }
}

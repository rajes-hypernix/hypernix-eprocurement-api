using FSH.Framework.Core.Exceptions;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;
using FSH.Modules.Suppliers.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Features.v1.Vendors.ToggleVendorStatus;

public sealed class ToggleVendorStatusCommandHandler(SuppliersDbContext dbContext)
    : ICommandHandler<ToggleVendorStatusCommand, Guid>
{
    public async ValueTask<Guid> Handle(ToggleVendorStatusCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vendor = await dbContext.Vendors
            .FirstOrDefaultAsync(v => v.Id == command.VendorId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Vendor {command.VendorId} not found.");

        vendor.ToggleActive();

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return vendor.Id;
    }
}

using FSH.Framework.Core.Exceptions;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;
using FSH.Modules.Suppliers.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Features.v1.Vendors.UpdateVendor;

public sealed class UpdateVendorCommandHandler(SuppliersDbContext dbContext)
    : ICommandHandler<UpdateVendorCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdateVendorCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vendor = await dbContext.Vendors
            .FirstOrDefaultAsync(v => v.Id == command.VendorId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Vendor {command.VendorId} not found.");

        vendor.UpdateProfile(
            command.Name,
            command.RegisteredName,
            command.RegistrationNo,
            command.TaxId,
            command.Type,
            command.LlrcTier,
            command.Region,
            command.State,
            command.City,
            command.PaymentTerms,
            command.CreditLimit,
            command.Rating);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return vendor.Id;
    }
}

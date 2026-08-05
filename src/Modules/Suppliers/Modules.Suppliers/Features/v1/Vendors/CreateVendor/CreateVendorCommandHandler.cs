using FSH.Modules.Suppliers.Contracts.v1.Vendors;
using FSH.Modules.Suppliers.Data;
using FSH.Modules.Suppliers.Domain;
using Mediator;

namespace FSH.Modules.Suppliers.Features.v1.Vendors.CreateVendor;

public sealed class CreateVendorCommandHandler(SuppliersDbContext dbContext, ISuppliersCodeGenerator codeGenerator)
    : ICommandHandler<CreateVendorCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateVendorCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        string code = await codeGenerator.NextVendorCodeAsync(cancellationToken).ConfigureAwait(false);
        var vendor = Vendor.Create(code, command.Name, command.Name);
        vendor.UpdateProfile(
            command.Name,
            command.Name,
            "—",
            "—",
            VendorType.NonSwec,
            null,
            command.Region ?? "Peninsular",
            command.State ?? "—",
            command.City ?? "—",
            "MY",
            null,
            null,
            "30 days nett",
            0m,
            0m);

        dbContext.Vendors.Add(vendor);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return vendor.Id;
    }
}

using FSH.Modules.Suppliers.Contracts.v1.Vendors;
using FSH.Modules.Suppliers.Data;
using FSH.Modules.Suppliers.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Features.v1.Vendors.CreateManualVendor;

public sealed class CreateManualVendorCommandHandler(SuppliersDbContext dbContext, ISuppliersCodeGenerator codeGenerator)
    : ICommandHandler<CreateManualVendorCommand, CreateManualVendorResult>
{
    public async ValueTask<CreateManualVendorResult> Handle(CreateManualVendorCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        string? duplicateWarning = await VendorDuplicateChecker.FindWarningAsync(dbContext, command.RegistrationNo, command.Name, cancellationToken)
            .ConfigureAwait(false);

        string code = await codeGenerator.NextVendorCodeAsync(cancellationToken).ConfigureAwait(false);
        var vendor = Vendor.CreateManual(
            code,
            command.Name,
            command.RegisteredName,
            command.RegistrationNo,
            command.TaxId,
            command.Type,
            command.Region,
            command.State,
            command.City,
            command.Country,
            command.PaymentTerms,
            command.Categories);

        vendor.AddCurrency(new VendorCurrency(command.Currency, true));

        if (!string.IsNullOrWhiteSpace(command.Bank))
        {
            vendor.AddBankAccount(new VendorBankAccount(command.Bank, command.AccountNo, command.Swift, command.Currency, true));
        }

        if (!string.IsNullOrWhiteSpace(command.ContactName) || !string.IsNullOrWhiteSpace(command.ContactEmail))
        {
            vendor.AddContact(new VendorContact(command.ContactName ?? command.Name, null, command.ContactEmail, null, true));
        }

        if (!string.IsNullOrWhiteSpace(command.AddressLine) || !string.IsNullOrWhiteSpace(command.City))
        {
            vendor.AddAddress(new VendorAddress("Registered", command.AddressLine, command.City, command.State, command.Country, null, true));
        }

        dbContext.Vendors.Add(vendor);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CreateManualVendorResult(vendor.Id, vendor.Code, duplicateWarning);
    }
}

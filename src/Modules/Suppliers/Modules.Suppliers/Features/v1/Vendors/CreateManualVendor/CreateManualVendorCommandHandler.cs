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
            VendorTypeParser.Parse(command.Type),
            command.Region,
            command.State,
            command.City,
            command.CountryCode,
            command.StateId,
            command.CityId,
            command.PaymentTerms,
            command.Categories);

        vendor.AddCurrency(new VendorCurrency(command.CurrencyCode, true));

        if (command.BankId is { } bankId && bankId != Guid.Empty && !string.IsNullOrWhiteSpace(command.BankName))
        {
            vendor.AddBankAccount(new VendorBankAccount(bankId, command.BankName, command.AccountNo, command.Swift, command.CurrencyCode, true));
        }

        if (!string.IsNullOrWhiteSpace(command.ContactName) || !string.IsNullOrWhiteSpace(command.ContactEmail))
        {
            vendor.AddContact(new VendorContact(command.ContactName ?? command.Name, null, command.ContactEmail, null, true));
        }

        if (!string.IsNullOrWhiteSpace(command.AddressLine) || !string.IsNullOrWhiteSpace(command.City))
        {
            vendor.AddAddress(new VendorAddress(VendorAddressType.Registered, command.AddressLine, command.City, command.State, command.CountryCode, command.StateId, command.CityId, null, true));
        }

        dbContext.Vendors.Add(vendor);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CreateManualVendorResult(vendor.Id, vendor.Code, duplicateWarning);
    }
}

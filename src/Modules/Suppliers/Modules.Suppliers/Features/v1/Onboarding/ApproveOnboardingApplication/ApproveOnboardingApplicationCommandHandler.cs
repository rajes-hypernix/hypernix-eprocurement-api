using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using FSH.Modules.Suppliers.Data;
using FSH.Modules.Suppliers.Domain;
using FSH.Modules.Suppliers.Features.v1.Vendors;
using FSH.Modules.Suppliers.Services.Onboarding;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.ApproveOnboardingApplication;

/// <summary>
/// Promotion order matters: the in-memory Vendor/VendorUser are fully built BEFORE
/// <c>application.Approve(...)</c> is called, so an illegal or replayed approval never partially
/// writes the master — nothing is added to the DbContext until the domain guard has passed.
/// </summary>
public sealed class ApproveOnboardingApplicationCommandHandler(
    SuppliersDbContext dbContext,
    ISuppliersCodeGenerator codeGenerator,
    ICurrentUser currentUser,
    IOnboardingNotifier notifier)
    : ICommandHandler<ApproveOnboardingApplicationCommand, OnboardingApproveResultDto>
{
    public async ValueTask<OnboardingApproveResultDto> Handle(ApproveOnboardingApplicationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var application = await dbContext.VendorOnboardingApplications
            .Include(a => a.Financial)
            .FirstOrDefaultAsync(a => a.Id == command.ApplicationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Onboarding application {command.ApplicationId} not found.");

        string? duplicateWarning = await VendorDuplicateChecker
            .FindWarningAsync(dbContext, application.RegistrationNo, application.Name, cancellationToken)
            .ConfigureAwait(false);

        string vendorCode = await codeGenerator.NextVendorCodeAsync(cancellationToken).ConfigureAwait(false);
        string registeredName = string.IsNullOrWhiteSpace(application.RegisteredName) ? application.Name : application.RegisteredName;
        var vendor = Vendor.Create(vendorCode, application.Name, registeredName);
        vendor.UpdateProfile(
            application.Name,
            registeredName,
            string.IsNullOrWhiteSpace(application.RegistrationNo) ? "—" : application.RegistrationNo,
            string.IsNullOrWhiteSpace(application.TaxId) ? "—" : application.TaxId,
            application.Type,
            null,
            application.Region,
            application.State,
            application.City,
            "NET30",
            0m,
            0m);
        vendor.SetCategories(application.Categories);

        PromoteContactsAndAddresses(application, vendor);
        foreach (var bank in application.BankAccounts)
        {
            vendor.AddBankAccount(new VendorBankAccount(bank.Bank, bank.AccountNo, bank.Swift, bank.Currency, bank.IsPrimary));
        }

        foreach (var certification in application.Certifications)
        {
            vendor.AddCertification(new VendorCertification(certification.Name, certification.Number, certification.ValidTo, certification.Status));
        }

        if (application.Type == "Swec")
        {
            vendor.Register();
        }
        else
        {
            vendor.MarkProvisional();
        }

        string vendorUserCode = await codeGenerator.NextVendorUserCodeAsync(cancellationToken).ConfigureAwait(false);
        string loginEmail = await UniqueLoginEmailAsync(application.Email, vendorCode, cancellationToken).ConfigureAwait(false);
        var vendorUser = VendorUser.Create(vendorUserCode, vendor.Id, $"{application.Name} — Portal", loginEmail);

        var now = DateTime.UtcNow;
        application.Approve(vendor.Id, currentUser.GetUserId().ToString(), currentUser.Name ?? "Buyer", now);
        application.Financial?.LinkToVendor(vendor.Id);

        if (application.InvitationId is { } invitationId)
        {
            var invitation = await dbContext.VendorOnboardingInvitations
                .FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken)
                .ConfigureAwait(false);
            invitation?.Complete();
        }

        dbContext.Vendors.Add(vendor);
        dbContext.VendorUsers.Add(vendorUser);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await notifier.SendApprovedAsync(application, vendorUser, cancellationToken).ConfigureAwait(false);

        return new OnboardingApproveResultDto(vendor.Id, vendor.Code, duplicateWarning);
    }

    private static void PromoteContactsAndAddresses(Domain.Onboarding.VendorOnboardingApplication application, Vendor vendor)
    {
        if (application.Contacts.Count > 0)
        {
            foreach (var contact in application.Contacts)
            {
                vendor.AddContact(new VendorContact(contact.Name, contact.Role, contact.Email, contact.Phone, contact.IsPrimary));
            }
        }
        else if (!string.IsNullOrWhiteSpace(application.ContactName) || !string.IsNullOrWhiteSpace(application.Email))
        {
            string name = string.IsNullOrWhiteSpace(application.ContactName) ? application.Name : application.ContactName;
            vendor.AddContact(new VendorContact(name, null, application.Email, application.ContactPhone, true));
        }

        if (application.Addresses.Count > 0)
        {
            foreach (var address in application.Addresses)
            {
                vendor.AddAddress(new VendorAddress(address.Type, address.Line, address.City, address.State, address.Country, address.Postcode, address.IsPrimary));
            }
        }
        else if (!string.IsNullOrWhiteSpace(application.City) || !string.IsNullOrWhiteSpace(application.State))
        {
            vendor.AddAddress(new VendorAddress("Registered", null, application.City, application.State, "MY", null, true));
        }
    }

    private async Task<string> UniqueLoginEmailAsync(string email, string vendorCode, CancellationToken cancellationToken)
    {
        bool collides = !string.IsNullOrWhiteSpace(email)
            && await dbContext.VendorUsers.AsNoTracking().AnyAsync(u => u.Email == email, cancellationToken).ConfigureAwait(false);

        return collides || string.IsNullOrWhiteSpace(email)
            ? $"{vendorCode.ToLowerInvariant()}@vendor.portal"
            : email;
    }
}

using System.Diagnostics;
using System.Security.Cryptography;
using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Eventing.Abstractions;
using FSH.Framework.Web.Origin;
using FSH.Modules.Communication.Contracts.Events;
using FSH.Modules.Identity.Contracts.DTOs;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using FSH.Modules.Suppliers.Data;
using FSH.Modules.Suppliers.Domain;
using FSH.Modules.Suppliers.Features.v1.Vendors;
using FSH.Modules.Suppliers.Services.Onboarding;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.ApproveOnboardingApplication;

/// <summary>
/// Promotion order matters: the in-memory Vendor/VendorUser are fully built BEFORE
/// <c>application.Approve(...)</c> is called, so an illegal or replayed approval never partially
/// writes the master — nothing is added to the DbContext until the domain guard has passed.
/// Vendor Identity provisioning (real FSH Identity login) happens AFTER the Suppliers rows are
/// committed: Suppliers state is the source of truth, and login provisioning is a best-effort
/// follow-up — a vendor with no login yet is recoverable, a login pointing at nothing is not.
/// </summary>
public sealed class ApproveOnboardingApplicationCommandHandler(
    SuppliersDbContext dbContext,
    ISuppliersCodeGenerator codeGenerator,
    ICurrentUser currentUser,
    IOnboardingNotifier notifier,
    IUserRegistrationService userRegistrationService,
    IUserRoleService userRoleService,
    IUserPasswordService userPasswordService,
    IOptions<OriginOptions> originOptions,
    IEventBus eventBus)
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
            application.CountryCode,
            application.StateId,
            application.CityId,
            "NET30",
            0m,
            0m);
        vendor.SetCategories(application.Categories);

        PromoteContactsAndAddresses(application, vendor);
        foreach (var bank in application.BankAccounts)
        {
            vendor.AddBankAccount(new VendorBankAccount(bank.BankId, bank.BankName, bank.AccountNo, bank.Swift, bank.CurrencyCode, bank.IsPrimary));
        }

        foreach (var certification in application.Certifications)
        {
            vendor.AddCertification(new VendorCertification(certification.Name, certification.Number, certification.ValidTo, certification.Status));
        }

        if (application.Type == VendorType.Swec)
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

        await ProvisionVendorLoginAsync(vendorUser, cancellationToken).ConfigureAwait(false);
        await notifier.SendApprovedAsync(application, vendorUser, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(vendorUser.IdentityUserId))
        {
            var correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString();
            await eventBus.PublishAsync(
                new OnboardingApprovedIntegrationEvent(
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    currentUser.GetTenant(),
                    correlationId,
                    "Suppliers",
                    vendor.Id,
                    vendor.Code,
                    vendorUser.IdentityUserId),
                cancellationToken).ConfigureAwait(false);
        }

        return new OnboardingApproveResultDto(vendor.Id, vendor.Code, duplicateWarning);
    }

    /// <summary>
    /// Provisions a real FSH Identity login for the vendor: registers a password-based user
    /// carrying <c>VendorId</c> (so their JWT gets a <c>vendorId</c> claim), skips the
    /// self-service email-confirmation step (the vendor never requested this registration),
    /// swaps the auto-assigned Basic role for the non-default Vendor role, and sends a real
    /// forgot-password email so the vendor sets their own password — replacing the old system's
    /// dead "set your password" link with Identity's existing, tested reset flow.
    /// </summary>
    private async Task ProvisionVendorLoginAsync(VendorUser vendorUser, CancellationToken cancellationToken)
    {
        var origin = originOptions.Value?.OriginUrl?.ToString();
        if (string.IsNullOrWhiteSpace(origin))
        {
            throw new InvalidOperationException("Origin URL is not configured.");
        }

        string tempPassword = GenerateTempPassword();
        string[] nameParts = vendorUser.Name.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        string firstName = nameParts.Length > 0 ? nameParts[0] : vendorUser.Name;
        string lastName = nameParts.Length > 1 ? nameParts[1] : string.Empty;

        string identityUserId = await userRegistrationService.RegisterAsync(
            firstName,
            lastName,
            vendorUser.Email,
            vendorUser.Code,
            tempPassword,
            tempPassword,
            phoneNumber: string.Empty,
            origin,
            cancellationToken,
            vendorId: vendorUser.VendorId).ConfigureAwait(false);

        await userRegistrationService.AdminConfirmEmailAsync(identityUserId, cancellationToken).ConfigureAwait(false);

        await userRoleService.AssignRolesAsync(
            identityUserId,
            [
                new UserRoleDto { RoleName = "Vendor", Enabled = true },
                new UserRoleDto { RoleName = "Basic", Enabled = false },
            ],
            cancellationToken).ConfigureAwait(false);

        await userPasswordService.ForgotPasswordAsync(vendorUser.Email, origin, cancellationToken).ConfigureAwait(false);

        vendorUser.LinkIdentity(identityUserId);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string GenerateTempPassword()
    {
        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))
            .Replace("+", "A", StringComparison.Ordinal)
            .Replace("/", "b", StringComparison.Ordinal)
            .Replace("=", string.Empty, StringComparison.Ordinal);
        return $"Tv1{token}"[..16];
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
                vendor.AddAddress(new VendorAddress(address.Type, address.Line, address.City, address.State, address.CountryCode, address.StateId, address.CityId, address.Postcode, address.IsPrimary));
            }
        }
        else if (!string.IsNullOrWhiteSpace(application.City) || !string.IsNullOrWhiteSpace(application.State))
        {
            vendor.AddAddress(new VendorAddress(VendorAddressType.Registered, null, application.City, application.State, application.CountryCode, application.StateId, application.CityId, null, true));
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

using System.Security.Cryptography;
using FSH.Framework.Core.Context;
using FSH.Modules.Platform.Contracts.Services;
using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using FSH.Modules.Suppliers.Data;
using FSH.Modules.Suppliers.Domain;
using FSH.Modules.Suppliers.Domain.Onboarding;
using FSH.Modules.Suppliers.Services.Onboarding;
using Mediator;
using Microsoft.Extensions.Options;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.CreateOnboardingInvitation;

public sealed class CreateOnboardingInvitationCommandHandler(
    SuppliersDbContext dbContext,
    ISuppliersCodeGenerator codeGenerator,
    IOnboardingNotifier notifier,
    ICurrentUser currentUser,
    IOptions<OnboardingOptions> options,
    IFormTemplateCatalog formTemplates)
    : ICommandHandler<CreateOnboardingInvitationCommand, OnboardingInvitationDto>
{
    public async ValueTask<OnboardingInvitationDto> Handle(CreateOnboardingInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var templateIds = command.SelectedTemplateIds ?? [];
        await formTemplates.EnsureActiveTemplatesAsync(templateIds, cancellationToken).ConfigureAwait(false);

        string email = string.IsNullOrWhiteSpace(command.Email) ? options.Value.DefaultVendorEmail : command.Email;
        string rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var now = DateTime.UtcNow;

        var invitation = VendorOnboardingInvitation.Create(
            email,
            VendorTypeParser.Parse(command.Type),
            templateIds,
            rawToken,
            currentUser.GetUserId().ToString(),
            currentUser.Name ?? "Buyer",
            now,
            options.Value.LinkExpiryDays);

        string code = await codeGenerator.NextOnboardingApplicationCodeAsync(cancellationToken).ConfigureAwait(false);
        var application = VendorOnboardingApplication.CreateFromInvitation(code, invitation, now);
        invitation.AttachApplication(application.Id);

        dbContext.VendorOnboardingInvitations.Add(invitation);
        dbContext.VendorOnboardingApplications.Add(application);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await notifier.SendInvitationAsync(invitation, rawToken, cancellationToken).ConfigureAwait(false);

        return OnboardingDtoMapper.ToDto(invitation, application.Code, notifier.BuildMagicLink(rawToken));
    }
}

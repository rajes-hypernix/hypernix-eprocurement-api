using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using FSH.Modules.Suppliers.Data;
using FSH.Modules.Suppliers.Services.Onboarding;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.RejectOnboardingApplication;

public sealed class RejectOnboardingApplicationCommandHandler(
    SuppliersDbContext dbContext,
    ICurrentUser currentUser,
    IOnboardingNotifier notifier)
    : ICommandHandler<RejectOnboardingApplicationCommand, OnboardingApplicationDto>
{
    public async ValueTask<OnboardingApplicationDto> Handle(RejectOnboardingApplicationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var application = await dbContext.VendorOnboardingApplications
            .FirstOrDefaultAsync(a => a.Id == command.ApplicationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Onboarding application {command.ApplicationId} not found.");

        application.Reject(command.Reason, currentUser.GetUserId().ToString(), currentUser.Name ?? "Buyer", DateTime.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await notifier.SendRejectedAsync(application, command.Reason, cancellationToken).ConfigureAwait(false);

        return OnboardingDtoMapper.ToDto(application);
    }
}

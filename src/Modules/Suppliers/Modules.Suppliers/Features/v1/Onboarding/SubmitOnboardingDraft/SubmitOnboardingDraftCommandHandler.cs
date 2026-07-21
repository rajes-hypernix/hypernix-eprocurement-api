using System.Diagnostics;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Core.Context;
using FSH.Framework.Eventing.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Communication.Contracts.Events;
using FSH.Modules.Platform.Contracts.Services;
using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using FSH.Modules.Suppliers.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.SubmitOnboardingDraft;

public sealed class SubmitOnboardingDraftCommandHandler(
    SuppliersDbContext dbContext,
    IFormTemplateCatalog formTemplates,
    IEventBus eventBus,
    ICurrentUser currentUser,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : ICommandHandler<SubmitOnboardingDraftCommand, OnboardingDraftDto>
{
    public async ValueTask<OnboardingDraftDto> Handle(SubmitOnboardingDraftCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var (_, application) = await OnboardingTokenGate.ResolveAsync(dbContext, command.Token, cancellationToken).ConfigureAwait(false);

        if (application.SelectedTemplateIds.Count > 0)
        {
            await formTemplates.ValidateAnswersAsync(
                application.SelectedTemplateIds,
                [.. application.Answers.Select(a => new FormAnswerInput(a.FormTemplateId, a.QuestionOrder, a.Value))],
                requireRequired: true,
                cancellationToken).ConfigureAwait(false);
        }

        application.Submit(DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        string? buyerUserId = null;
        if (application.InvitationId is { } invitationId)
        {
            buyerUserId = await dbContext.VendorOnboardingInvitations
                .AsNoTracking()
                .Where(i => i.Id == invitationId)
                .Select(i => i.InvitedByUserId)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(buyerUserId))
        {
            // Token submit has no JWT tenant claim — fall back to Finbuckle ambient (request header).
            var tenantId = currentUser.GetTenant();
            if (string.IsNullOrWhiteSpace(tenantId))
                tenantId = tenantAccessor.MultiTenantContext.TenantInfo?.Id;
            var correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString();
            await eventBus.PublishAsync(
                new OnboardingSubmittedIntegrationEvent(
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    tenantId,
                    correlationId,
                    "Suppliers",
                    application.Id,
                    application.Code,
                    string.IsNullOrWhiteSpace(application.Name) ? application.Email : application.Name,
                    buyerUserId),
                cancellationToken).ConfigureAwait(false);
        }

        return OnboardingDtoMapper.ToDraftDto(application);
    }
}

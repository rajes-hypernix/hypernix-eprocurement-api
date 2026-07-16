using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using FSH.Modules.Suppliers.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.ListOnboardingInvitations;

public sealed class ListOnboardingInvitationsQueryHandler(SuppliersDbContext dbContext)
    : IQueryHandler<ListOnboardingInvitationsQuery, IReadOnlyList<OnboardingInvitationDto>>
{
    public async ValueTask<IReadOnlyList<OnboardingInvitationDto>> Handle(ListOnboardingInvitationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var invitations = await dbContext.VendorOnboardingInvitations
            .AsNoTracking()
            .OrderByDescending(i => i.CreatedUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var applicationCodes = await dbContext.VendorOnboardingApplications
            .AsNoTracking()
            .Where(a => invitations.Select(i => i.ApplicationId).Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Code, cancellationToken)
            .ConfigureAwait(false);

        return [.. invitations.Select(i => OnboardingDtoMapper.ToDto(
            i,
            i.ApplicationId is { } appId && applicationCodes.TryGetValue(appId, out string? code) ? code : null,
            magicLink: null))];
    }
}

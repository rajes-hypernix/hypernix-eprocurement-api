using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Clarifications;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Clarifications.GetClarificationThread;

public sealed class GetClarificationThreadQueryHandler(
    SourcingDbContext dbContext,
    ICurrentUser currentUser,
    IUserService userService)
    : IQueryHandler<GetClarificationThreadQuery, IReadOnlyList<ClarificationMessageDto>>
{
    public async ValueTask<IReadOnlyList<ClarificationMessageDto>> Handle(GetClarificationThreadQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var vendorId = currentUser.GetVendorId();
        if (vendorId is { } ownVendorId)
        {
            if (ownVendorId != query.VendorId)
            {
                // 404, not 403 — a vendor can't probe another vendor's thread.
                throw new NotFoundException("Clarification thread not found.");
            }
        }
        else
        {
            bool allowed = await userService
                .HasPermissionAsync(currentUser.GetUserId().ToString(), SourcingPermissions.Clarifications.View, cancellationToken)
                .ConfigureAwait(false);
            if (!allowed)
            {
                throw new ForbiddenException("Clarifications view permission is required.");
            }
        }

        var messages = await dbContext.Clarifications
            .Where(c => c.Scope == query.Scope && c.VendorId == query.VendorId)
            .OrderBy(c => c.CreatedUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var message in messages)
        {
            if (vendorId is not null)
            {
                message.MarkReadByVendor();
            }
            else
            {
                message.MarkReadByBuyer();
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return [.. messages.Select(ClarificationDtoMapper.ToDto)];
    }
}

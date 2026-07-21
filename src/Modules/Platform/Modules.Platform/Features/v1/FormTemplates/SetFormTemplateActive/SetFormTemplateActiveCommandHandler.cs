using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.FormTemplates;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.FormTemplates.SetFormTemplateActive;

public sealed class SetFormTemplateActiveCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<SetFormTemplateActiveCommand, Guid>
{
    public async ValueTask<Guid> Handle(SetFormTemplateActiveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var template = await dbContext.FormTemplates.FirstOrDefaultAsync(t => t.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Form template {command.Id} not found.");

        template.SetActive(command.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return template.Id;
    }
}

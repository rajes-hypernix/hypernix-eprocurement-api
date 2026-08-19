using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.FormTemplates;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.FormTemplates.UpdateFormTemplate;

public sealed class UpdateFormTemplateCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<UpdateFormTemplateCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdateFormTemplateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var template = await dbContext.FormTemplates
            .Include(t => t.Questions)
            .FirstOrDefaultAsync(t => t.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Form template {command.Id} not found.");

        template.Rename(command.Name);
        template.ReplaceQuestions(
            command.Questions.Select(q => (q.Order, q.Label, q.Type, q.Required, q.ConfigJson, q.Help)));

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return template.Id;
    }
}

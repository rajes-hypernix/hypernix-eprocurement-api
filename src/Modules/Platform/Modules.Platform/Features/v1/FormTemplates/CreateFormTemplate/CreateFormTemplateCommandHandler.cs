using FSH.Modules.Platform.Contracts.v1.FormTemplates;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.FormTemplates.CreateFormTemplate;

public sealed class CreateFormTemplateCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<CreateFormTemplateCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateFormTemplateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var key = command.Key.Trim();
        bool exists = await dbContext.FormTemplates.AnyAsync(t => t.Key == key, cancellationToken).ConfigureAwait(false);
        if (exists)
            throw new PlatformRuleException($"Form template '{key}' already exists.");

        var template = FormTemplate.Create(command.Key, command.Name);
        foreach (var q in command.Questions.OrderBy(x => x.Order))
            template.AddQuestion(q.Order, q.Label, q.Type, q.Required, q.ConfigJson, q.Help);

        dbContext.FormTemplates.Add(template);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return template.Id;
    }
}

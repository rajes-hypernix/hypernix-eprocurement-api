using FSH.Modules.Platform.Contracts.Services;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Services;

public sealed class ReasonCodeValidator(PlatformDbContext dbContext) : IReasonCodeValidator
{
    public async Task EnsureActiveAsync(string listKey, string reasonCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);

        var key = listKey.Trim();
        var code = reasonCode.Trim().ToUpperInvariant();

        var list = await dbContext.CustomLists
            .AsNoTracking()
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.Key == key && l.IsActive, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new PlatformRuleException($"Custom list '{key}' is not available.");

        bool ok = list.Items.Any(i =>
            i.IsActive && string.Equals(i.Code, code, StringComparison.OrdinalIgnoreCase));

        if (!ok)
            throw new PlatformRuleException($"Reason code '{code}' is not valid for list '{key}'.");
    }
}

public sealed class FormTemplateCatalog(PlatformDbContext dbContext) : IFormTemplateCatalog
{
    public async Task EnsureActiveTemplatesAsync(IReadOnlyList<Guid> templateIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(templateIds);
        if (templateIds.Count == 0)
            return;

        var distinct = templateIds.Distinct().ToList();
        var found = await dbContext.FormTemplates
            .AsNoTracking()
            .Where(t => distinct.Contains(t.Id) && t.IsActive)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var missing = distinct.Except(found).ToList();
        if (missing.Count > 0)
            throw new PlatformRuleException($"Unknown or inactive form template(s): {string.Join(", ", missing)}.");
    }

    public async Task ValidateAnswersAsync(
        IReadOnlyList<Guid> selectedTemplateIds,
        IReadOnlyList<FormAnswerInput> answers,
        bool requireRequired,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selectedTemplateIds);
        ArgumentNullException.ThrowIfNull(answers);

        await EnsureActiveTemplatesAsync(selectedTemplateIds, cancellationToken).ConfigureAwait(false);

        var templates = await dbContext.FormTemplates
            .AsNoTracking()
            .Include(t => t.Questions)
            .Where(t => selectedTemplateIds.Contains(t.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byTemplate = templates.ToDictionary(
            t => t.Id,
            t => t.Questions.ToDictionary(q => q.Order));

        foreach (var answer in answers)
        {
            if (!selectedTemplateIds.Contains(answer.FormTemplateId))
                throw new PlatformRuleException($"Answer references template {answer.FormTemplateId} not selected on the invitation.");

            if (!byTemplate.TryGetValue(answer.FormTemplateId, out var questions)
                || !questions.ContainsKey(answer.QuestionOrder))
            {
                throw new PlatformRuleException(
                    $"Unknown question order {answer.QuestionOrder} on template {answer.FormTemplateId}.");
            }
        }

        if (!requireRequired)
            return;

        var answered = answers
            .Where(a => !string.IsNullOrWhiteSpace(a.Value))
            .Select(a => (a.FormTemplateId, a.QuestionOrder))
            .ToHashSet();

        foreach (var template in templates)
        {
            foreach (var question in template.Questions.Where(q => q.Required))
            {
                if (!answered.Contains((template.Id, question.Order)))
                    throw new PlatformRuleException($"Required question {question.Order} on template {template.Id} is unanswered.");
            }
        }
    }
}
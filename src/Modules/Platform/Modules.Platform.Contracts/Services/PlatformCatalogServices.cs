namespace FSH.Modules.Platform.Contracts.Services;

public interface IReasonCodeValidator
{
    /// <summary>Ensures <paramref name="reasonCode"/> is an active item on the custom list; throws on miss.</summary>
    Task EnsureActiveAsync(string listKey, string reasonCode, CancellationToken cancellationToken = default);
}

public interface IFormTemplateCatalog
{
    Task EnsureActiveTemplatesAsync(IReadOnlyList<Guid> templateIds, CancellationToken cancellationToken = default);

    /// <summary>Validates answers against the selected form templates.</summary>
    /// <param name="selectedTemplateIds">Templates attached to the invitation/application.</param>
    /// <param name="answers">Submitted answers.</param>
    /// <param name="requireRequired">When true, every Required question must have a non-blank answer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ValidateAnswersAsync(
        IReadOnlyList<Guid> selectedTemplateIds,
        IReadOnlyList<FormAnswerInput> answers,
        bool requireRequired,
        CancellationToken cancellationToken = default);
}

public sealed record FormAnswerInput(Guid FormTemplateId, int QuestionOrder, string Value);

using FSH.Modules.Platform.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Platform.Contracts.v1.FormTemplates;

public sealed record ListFormTemplatesQuery(bool ActiveOnly = true)
    : IQuery<IReadOnlyList<FormTemplateListItemDto>>;

public sealed record GetFormTemplateQuery(Guid Id) : IQuery<FormTemplateDto?>;

public sealed record ListFormTemplatesByIdsQuery(IReadOnlyList<Guid> Ids)
    : IQuery<IReadOnlyList<FormTemplateDto>>;

public sealed record CreateFormTemplateCommand(
    string Key,
    string Name,
    IReadOnlyList<CreateFormTemplateQuestionDto> Questions) : ICommand<Guid>;

public sealed record CreateFormTemplateQuestionDto(
    int Order,
    string Label,
    string Type,
    bool Required,
    string? ConfigJson = null,
    string? Help = null);

public sealed record SetFormTemplateActiveCommand(Guid Id, bool IsActive) : ICommand<Guid>;

public sealed record UpdateFormTemplateCommand(
    Guid Id,
    string Name,
    IReadOnlyList<CreateFormTemplateQuestionDto> Questions) : ICommand<Guid>;

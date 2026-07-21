using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Domain;

namespace FSH.Modules.Platform.Features.v1;

internal static class PlatformDtoMapper
{
    internal static OrgUnitDto ToDto(OrgUnit unit) =>
        new(unit.Id, unit.Code, unit.Name, unit.Type.ToString(), unit.ParentId, unit.IsActive);

    internal static FormTemplateQuestionDto ToDto(FormTemplateQuestion q) =>
        new(q.Id, q.Order, q.Label, q.Type, q.Required, q.ConfigJson, q.Help);

    internal static FormTemplateDto ToDto(FormTemplate template) =>
        new(
            template.Id,
            template.Key,
            template.Name,
            template.IsActive,
            [.. template.Questions.OrderBy(q => q.Order).Select(ToDto)]);

    internal static FormTemplateListItemDto ToListItem(FormTemplate template) =>
        new(template.Id, template.Key, template.Name, template.IsActive, template.Questions.Count);
}

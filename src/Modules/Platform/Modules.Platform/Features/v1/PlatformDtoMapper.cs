using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Domain;

namespace FSH.Modules.Platform.Features.v1;

internal static class PlatformDtoMapper
{
    internal static OrgUnitDto ToDto(OrgUnit unit) =>
        new(unit.Id, unit.Code, unit.Name, unit.Type.ToString(), unit.ParentId, unit.IsActive, unit.CreatedOnUtc);

    internal static FormTemplateQuestionDto ToDto(FormTemplateQuestion q) =>
        new(q.Id, q.Order, q.Label, q.Type, q.Required, q.ConfigJson, q.Help);

    internal static FormTemplateDto ToDto(FormTemplate template) =>
        new(
            template.Id,
            template.Key,
            template.Name,
            template.IsActive,
            [.. template.Questions.OrderBy(q => q.Order).Select(ToDto)]);

    internal static SavedViewFilterDto ToDto(SavedViewFilter filter) =>
        new(filter.FieldKey, filter.Operator.ToString(), filter.GroupIndex, filter.Value, filter.Value2, filter.Sort);

    internal static SavedViewColumnDto ToDto(SavedViewColumn column) =>
        new(column.FieldKey, column.Label, column.Sort, column.SortDirection?.ToString());

    internal static SavedViewDto ToDto(SavedView view) =>
        new(
            view.Id,
            view.Code,
            view.Name,
            view.RecordType.ToString(),
            view.OwnerUserId,
            view.IsShared,
            view.IsSystem,
            [.. view.Filters.OrderBy(f => f.Sort).Select(ToDto)],
            [.. view.Columns.OrderBy(c => c.Sort).Select(ToDto)],
            view.CreatedOnUtc,
            view.LastModifiedOnUtc);

    internal static ViewFieldDto ToDto(FieldRegistryEntry entry) =>
        new(entry.FieldKey, entry.Kind.ToString(), entry.Label, entry.DataType.ToString());

    internal static FormTemplateListItemDto ToListItem(FormTemplate template) =>
        new(template.Id, template.Key, template.Name, template.IsActive, template.Questions.Count);
}

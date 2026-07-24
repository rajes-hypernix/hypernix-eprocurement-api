using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Domain;

namespace FSH.Modules.Platform.Features.v1;

internal static class CustomFieldMapping
{
    internal static T ParseEnum<T>(string value, string label) where T : struct, Enum
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!Enum.TryParse<T>(value, ignoreCase: true, out var parsed))
        {
            throw new PlatformRuleException($"Unknown {label} '{value}'.");
        }

        return parsed;
    }

    internal static CustomFieldDefDto ToDto(CustomFieldDef def) => new(
        def.Id,
        def.Code,
        def.Label,
        def.DataType.ToString(),
        def.RefEntity?.ToString(),
        def.ListKey,
        def.Scope.ToString(),
        def.DisplayType.ToString(),
        def.ShowInList,
        def.IsRequired,
        def.HelpText,
        def.IsActive,
        [.. def.Applications.Select(a => a.RecordType.ToString())]);

    /// <summary>Phase 7: how a Custom field's own type maps onto the coarser Saved-Views operator/input
    /// vocabulary when its <c>FieldRegistryEntry</c> row is synced on Apply/Remove.</summary>
    internal static ViewFieldDataType ToViewFieldDataType(CustomFieldDataType dataType) => dataType switch
    {
        CustomFieldDataType.Money => ViewFieldDataType.Money,
        CustomFieldDataType.Int or CustomFieldDataType.Decimal or CustomFieldDataType.Percent => ViewFieldDataType.Number,
        CustomFieldDataType.Date => ViewFieldDataType.Date,
        CustomFieldDataType.DateTime => ViewFieldDataType.Instant,
        CustomFieldDataType.Bool => ViewFieldDataType.Bool,
        _ => ViewFieldDataType.Text,
    };

    internal static CustomFieldValueDto ToValueDto(CustomFieldDef def, CustomFieldValue? value) => new(
        def.Id,
        def.Code,
        def.Label,
        def.DataType.ToString(),
        def.DisplayType.ToString(),
        def.IsRequired,
        def.ListKey,
        def.RefEntity?.ToString(),
        value?.LineId,
        value?.ValueText,
        value?.ValueNumber,
        value?.ValueDate,
        value?.ValueDateTime,
        value?.ValueBool,
        value?.ValueListCode,
        value?.ValueRefId,
        value?.ValueLabel);
}

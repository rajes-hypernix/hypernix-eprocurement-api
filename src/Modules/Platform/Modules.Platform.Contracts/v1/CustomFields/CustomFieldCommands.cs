using FSH.Modules.Platform.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Platform.Contracts.v1.CustomFields;

public sealed record ListCustomFieldDefsQuery(string? RecordType = null) : IQuery<IReadOnlyList<CustomFieldDefDto>>;

public sealed record CreateCustomFieldDefCommand(
    string Code,
    string Label,
    string DataType,
    string Scope,
    string? RefEntity = null,
    string? ListKey = null,
    string DisplayType = "Normal",
    bool ShowInList = false,
    bool IsRequired = false,
    string? HelpText = null) : ICommand<Guid>;

public sealed record UpdateCustomFieldDefCommand(
    Guid Id,
    string Label,
    string DisplayType,
    bool ShowInList,
    bool IsRequired,
    string? HelpText) : ICommand<Guid>;

public sealed record SetCustomFieldDefActiveCommand(Guid Id, bool IsActive) : ICommand<Guid>;

public sealed record ApplyCustomFieldToRecordTypeCommand(Guid Id, string RecordType) : ICommand<Guid>;

public sealed record RemoveCustomFieldApplicationCommand(Guid Id, string RecordType) : ICommand<Guid>;

public sealed record GetCustomFieldValuesQuery(string RecordType, Guid RecordId) : IQuery<IReadOnlyList<CustomFieldValueDto>>;

public sealed record SetCustomFieldValuesCommand(string RecordType, Guid RecordId, IReadOnlyList<CustomFieldValueInput> Values) : ICommand;

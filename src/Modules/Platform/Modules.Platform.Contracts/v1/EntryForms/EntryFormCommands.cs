using FSH.Modules.Platform.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Platform.Contracts.v1.EntryForms;

public sealed record EntryFormGroupInputDto(string Title, int Sort);

public sealed record EntryFormFieldInputDto(string FieldKey, int GroupIndex, int Sort, bool RequiredOnForm, bool FullWidth);

public sealed record ListEntryFormsQuery(string? RecordType = null) : IQuery<IReadOnlyList<EntryFormListItemDto>>;

public sealed record GetEntryFormQuery(Guid Id) : IQuery<EntryFormDetailDto>;

public sealed record CreateEntryFormCommand(string Code, string Name, string RecordType, bool IsSystem = false) : ICommand<Guid>;

public sealed record UpdateEntryFormDetailsCommand(Guid Id, string Name) : ICommand<Guid>;

public sealed record SetEntryFormActiveCommand(Guid Id, bool IsActive) : ICommand<Guid>;

public sealed record ReplaceEntryFormLayoutCommand(
    Guid Id,
    IReadOnlyList<EntryFormGroupInputDto> Groups,
    IReadOnlyList<EntryFormFieldInputDto> Fields) : ICommand<Guid>;

public sealed record UpsertEntryFormRoleMapCommand(string RecordType, string Role, Guid EntryFormDefId) : ICommand<Guid>;

public sealed record ListEntryFormRoleMapsQuery(string? RecordType = null) : IQuery<IReadOnlyList<EntryFormRoleMapDto>>;

/// <summary>Resolves the form a role sees for a record type: a role-specific mapping if one exists, else the record type's IsSystem default.</summary>
public sealed record GetEntryFormForRoleQuery(string RecordType, string Role) : IQuery<ResolvedEntryFormDto>;

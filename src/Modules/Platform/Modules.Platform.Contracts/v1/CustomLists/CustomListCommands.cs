using FSH.Modules.Platform.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Platform.Contracts.v1.CustomLists;

public sealed record ListCustomListsQuery(bool ActiveOnly = true) : IQuery<IReadOnlyList<CustomListDto>>;

public sealed record ListCustomListItemsQuery(string ListKey, bool ActiveOnly = true)
    : IQuery<IReadOnlyList<CustomListItemDto>>;

public sealed record CreateCustomListCommand(string Key, string Name) : ICommand<Guid>;

public sealed record UpsertCustomListItemCommand(
    string ListKey,
    string Code,
    string Label,
    int SortOrder = 0,
    bool IsActive = true) : ICommand<Guid>;

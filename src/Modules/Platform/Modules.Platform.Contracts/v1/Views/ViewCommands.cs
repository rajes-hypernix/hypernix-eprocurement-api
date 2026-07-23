using FSH.Modules.Platform.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Platform.Contracts.v1.Views;

public sealed record ListSavedViewsQuery(string? RecordType) : IQuery<IReadOnlyList<SavedViewDto>>;

/// <summary>The ViewBuilder's field palette for a record type — the registry rows only.</summary>
public sealed record GetViewFieldsQuery(string RecordType) : IQuery<IReadOnlyList<ViewFieldDto>>;

public sealed record CreateSavedViewCommand(SaveViewRequest Request) : ICommand<SavedViewDto>;

public sealed record UpdateSavedViewCommand(Guid Id, SaveViewRequest Request) : ICommand<SavedViewDto>;

public sealed record DeleteSavedViewCommand(Guid Id) : ICommand<Unit>;

public sealed record ShareSavedViewCommand(Guid Id, bool IsShared) : ICommand<SavedViewDto>;

/// <summary><paramref name="Size"/> is capped at 200 by the handler regardless of what's requested.</summary>
public sealed record RunSavedViewQuery(Guid Id, int Page, int Size) : IQuery<ViewRunResult>;

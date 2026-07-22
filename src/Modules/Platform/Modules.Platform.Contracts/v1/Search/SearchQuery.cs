namespace FSH.Modules.Platform.Contracts.v1.Search;

/// <summary>Cross-module global search — handler lives in Host (touches multiple DbContexts).</summary>
public sealed record SearchQuery(string Q);

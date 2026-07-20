namespace FSH.Modules.Sourcing.Contracts.Dtos;

public sealed record FormItemDto(
    string Kind,
    string Group,
    string Section,
    string Label,
    string Type,
    bool Required,
    string? ConfigJson,
    string? Help,
    int Order);

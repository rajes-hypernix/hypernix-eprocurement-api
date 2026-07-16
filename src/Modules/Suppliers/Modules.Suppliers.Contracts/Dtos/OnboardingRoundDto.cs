namespace FSH.Modules.Suppliers.Contracts.Dtos;

public sealed record OnboardingRoundItemDto(string Topic, string Request, string Response);

public sealed record OnboardingRoundDto(
    int RoundNo,
    string Direction,
    string Status,
    string Message,
    string RaisedByName,
    DateTime RaisedUtc,
    DateTime? RespondedUtc,
    IReadOnlyList<OnboardingRoundItemDto> Items);

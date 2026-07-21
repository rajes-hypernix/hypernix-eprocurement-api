using FSH.Modules.Platform.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Platform.Contracts.v1.Banks;

public sealed record ListBanksQuery(string? CountryCode = null, bool ActiveOnly = true) : IQuery<IReadOnlyList<BankDto>>;

public sealed record CreateBankCommand(string Name, string CountryCode, string? SwiftCode = null) : ICommand<Guid>;

public sealed record SetBankActiveCommand(Guid Id, bool IsActive) : ICommand<Guid>;

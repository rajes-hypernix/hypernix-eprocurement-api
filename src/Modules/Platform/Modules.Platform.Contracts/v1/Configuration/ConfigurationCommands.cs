using FSH.Modules.Platform.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Platform.Contracts.v1.Configuration;

// ---- Settings ----
public sealed record ListSettingsQuery : IQuery<IReadOnlyList<SettingDto>>;
public sealed record UpdateSettingCommand(string Key, string Value) : ICommand<Guid>;

// ---- Currencies ----
public sealed record ListCurrenciesQuery(bool ActiveOnly = true) : IQuery<IReadOnlyList<CurrencyDto>>;
public sealed record CreateCurrencyCommand(string Code, string Name, string Symbol, int Decimals = 2) : ICommand<Guid>;
public sealed record UpdateCurrencyCommand(Guid Id, string Name, string Symbol, int Decimals) : ICommand<Guid>;
public sealed record SetCurrencyActiveCommand(Guid Id, bool IsActive) : ICommand<Guid>;

// ---- Exchange rates ----
public sealed record ListCurrentExchangeRatesQuery : IQuery<IReadOnlyList<ExchangeRateCurrentDto>>;
public sealed record ListExchangeRateHistoryQuery(string CurrencyCode) : IQuery<IReadOnlyList<ExchangeRateDto>>;
public sealed record AppendExchangeRateCommand(string CurrencyCode, decimal RateToBase, DateOnly EffectiveDate) : ICommand<Guid>;

// ---- Tax codes ----
public sealed record ListTaxCodesQuery(bool ActiveOnly = true) : IQuery<IReadOnlyList<TaxCodeDto>>;
public sealed record CreateTaxCodeCommand(string Code, string Name, decimal RatePct) : ICommand<Guid>;
public sealed record UpdateTaxCodeCommand(Guid Id, string Name, decimal RatePct) : ICommand<Guid>;
public sealed record SetTaxCodeActiveCommand(Guid Id, bool IsActive) : ICommand<Guid>;

// ---- Payment terms ----
public sealed record ListPaymentTermsQuery(bool ActiveOnly = true) : IQuery<IReadOnlyList<PaymentTermDto>>;
public sealed record GetPaymentTermQuery(Guid Id) : IQuery<PaymentTermDto>;
public sealed record CreatePaymentTermCommand(
    string Code,
    string Name,
    string Kind,
    int? DueDays = null,
    int? DayOfMonth = null,
    int? MonthsAhead = null,
    int? MinimumDaysBeforeDue = null,
    decimal? DiscountPct = null,
    int? DiscountDays = null,
    IReadOnlyList<PaymentScheduleRowInput>? Rows = null) : ICommand<Guid>;
public sealed record UpdatePaymentTermCommand(
    Guid Id,
    string Name,
    string Kind,
    int? DueDays = null,
    int? DayOfMonth = null,
    int? MonthsAhead = null,
    int? MinimumDaysBeforeDue = null,
    decimal? DiscountPct = null,
    int? DiscountDays = null,
    IReadOnlyList<PaymentScheduleRowInput>? Rows = null) : ICommand<Guid>;
public sealed record SetPaymentTermActiveCommand(Guid Id, bool IsActive) : ICommand<Guid>;
public sealed record ComputePaymentScheduleQuery(Guid Id, DateOnly BaseDate) : IQuery<IReadOnlyList<ScheduleInstalmentDto>>;
public sealed record PaymentScheduleRowInput(int Seq, decimal Percent, string Basis, int? Days = null, string? Label = null);

// ---- Incoterms ----
public sealed record ListIncotermsQuery(bool ActiveOnly = true) : IQuery<IReadOnlyList<IncotermDto>>;
public sealed record CreateIncotermCommand(string Code, string Name) : ICommand<Guid>;
public sealed record UpdateIncotermCommand(Guid Id, string Name) : ICommand<Guid>;
public sealed record SetIncotermActiveCommand(Guid Id, bool IsActive) : ICommand<Guid>;

// ---- Locations ----
public sealed record ListLocationsQuery(bool ActiveOnly = true) : IQuery<IReadOnlyList<LocationDto>>;
public sealed record GetLocationQuery(Guid Id) : IQuery<LocationDto>;
public sealed record CreateLocationCommand(string Code, string Name, IReadOnlyList<LocationAddressInput> Addresses) : ICommand<Guid>;
public sealed record UpdateLocationCommand(Guid Id, string Name, IReadOnlyList<LocationAddressInput> Addresses) : ICommand<Guid>;
public sealed record SetLocationActiveCommand(Guid Id, bool IsActive) : ICommand<Guid>;
public sealed record LocationAddressInput(
    string Label,
    string Line1,
    string City,
    string State,
    string Postcode,
    string? Line2 = null,
    string? Country = "MY",
    bool IsDefault = false,
    int? Sort = null);

// ---- Items ----
public sealed record ListItemsQuery(bool ActiveOnly = true) : IQuery<IReadOnlyList<ItemDto>>;
public sealed record CreateItemCommand(string ItemCode, string Description, string? Uom = null) : ICommand<Guid>;
public sealed record UpdateItemCommand(Guid Id, string ItemCode, string Description, string Uom) : ICommand<Guid>;
public sealed record SetItemActiveCommand(Guid Id, bool IsActive) : ICommand<Guid>;
public sealed record DeleteItemCommand(Guid Id) : ICommand<Guid>;

// ---- Numbering ----
public sealed record ListNumberingSchemesQuery : IQuery<IReadOnlyList<NumberingSchemeDto>>;
public sealed record UpdateNumberingSchemeCommand(string RecordType, string Prefix, bool YearSegment, int Digits) : ICommand<Guid>;
public sealed record PeekDocumentNumberQuery(string RecordType) : IQuery<string>;
public sealed record MintDocumentNumberCommand(string RecordType) : ICommand<string>;

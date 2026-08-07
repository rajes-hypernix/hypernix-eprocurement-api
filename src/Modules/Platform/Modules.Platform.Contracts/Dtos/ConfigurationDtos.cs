namespace FSH.Modules.Platform.Contracts.Dtos;

public sealed record SettingDto(
    Guid Id,
    string Key,
    string Value,
    string ValueKind,
    string Label,
    string? Description,
    DateTimeOffset CreatedOnUtc);

public sealed record CurrencyDto(
    Guid Id,
    string Code,
    string Name,
    string Symbol,
    int Decimals,
    bool IsActive,
    DateTimeOffset CreatedOnUtc);

public sealed record ExchangeRateDto(
    Guid Id,
    string CurrencyCode,
    decimal RateToBase,
    DateOnly EffectiveDate,
    string EnteredByUserId,
    DateTimeOffset CreatedOnUtc);

public sealed record ExchangeRateCurrentDto(
    string CurrencyCode,
    string CurrencyName,
    decimal? RateToBase,
    DateOnly? EffectiveDate,
    bool IsBaseCurrency);

public sealed record TaxCodeDto(Guid Id, string Code, string Name, decimal RatePct, bool IsActive, DateTimeOffset CreatedOnUtc);

public sealed record IncotermDto(Guid Id, string Code, string Name, bool IsActive, DateTimeOffset CreatedOnUtc);

public sealed record ItemDto(Guid Id, string ItemCode, string Description, string Uom, bool IsActive, DateTimeOffset CreatedOnUtc);

public sealed record PaymentScheduleRowDto(
    Guid Id,
    int Seq,
    decimal Percent,
    string Basis,
    int? Days,
    string? Label);

public sealed record PaymentTermDto(
    Guid Id,
    string Code,
    string Name,
    string Kind,
    bool IsActive,
    int? DueDays,
    int? DayOfMonth,
    int? MonthsAhead,
    int? MinimumDaysBeforeDue,
    decimal? DiscountPct,
    int? DiscountDays,
    IReadOnlyList<PaymentScheduleRowDto> Rows,
    DateTimeOffset CreatedOnUtc);

public sealed record LocationAddressDto(
    Guid Id,
    string Label,
    string Line1,
    string? Line2,
    string City,
    string State,
    string Postcode,
    string Country,
    bool IsDefault,
    int Sort);

public sealed record LocationDto(
    Guid Id,
    string Code,
    string Name,
    bool IsActive,
    IReadOnlyList<LocationAddressDto> Addresses,
    DateTimeOffset CreatedOnUtc);

public sealed record NumberingSchemeDto(
    Guid Id,
    string RecordType,
    string Prefix,
    bool YearSegment,
    int Digits,
    string PreviewExample,
    DateTimeOffset CreatedOnUtc);

public sealed record ScheduleInstalmentDto(
    DateOnly? DueDate,
    decimal Percent,
    DateOnly? DiscountDate,
    decimal? DiscountPct,
    string? Label);

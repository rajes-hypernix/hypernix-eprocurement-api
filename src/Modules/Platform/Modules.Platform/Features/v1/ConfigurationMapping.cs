using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Domain;

namespace FSH.Modules.Platform.Features.v1;

internal static class ConfigurationMapping
{
    internal static PaymentTermDto ToDto(PaymentTerm term) => new(
        term.Id,
        term.Code,
        term.Name,
        term.Kind.ToString(),
        term.IsActive,
        term.DueDays,
        term.DayOfMonth,
        term.MonthsAhead,
        term.MinimumDaysBeforeDue,
        term.DiscountPct,
        term.DiscountDays,
        [.. term.Rows.OrderBy(r => r.Seq).Select(ToDto)]);

    internal static PaymentScheduleRowDto ToDto(PaymentScheduleRow row) => new(
        row.Id,
        row.Seq,
        row.Percent,
        row.Basis.ToString(),
        row.Days,
        row.Label);

    internal static LocationDto ToDto(Location location) => new(
        location.Id,
        location.Code,
        location.Name,
        location.IsActive,
        [.. location.Addresses.OrderBy(a => a.Sort).Select(ToDto)]);

    internal static LocationAddressDto ToDto(LocationAddress address) => new(
        address.Id,
        address.Label,
        address.Line1,
        address.Line2,
        address.City,
        address.State,
        address.Postcode,
        address.Country,
        address.IsDefault,
        address.Sort);

    internal static ScheduleInstalmentDto ToDto(PaymentTermEngine.ScheduleInstalment instalment) => new(
        instalment.DueDate,
        instalment.Percent,
        instalment.DiscountDate,
        instalment.DiscountPct,
        instalment.Label);

    internal static IEnumerable<PaymentScheduleRowDraft> ToDrafts(IReadOnlyList<PaymentScheduleRowInput>? rows) =>
        (rows ?? []).Select(r => new PaymentScheduleRowDraft(
            r.Seq,
            r.Percent,
            ParseBasis(r.Basis),
            r.Days,
            r.Label));

    internal static IEnumerable<LocationAddressDraft> ToDrafts(IReadOnlyList<LocationAddressInput> addresses) =>
        addresses.Select(a => new LocationAddressDraft(
            a.Label,
            a.Line1,
            a.City,
            a.State,
            a.Postcode,
            a.Line2,
            a.Country,
            a.IsDefault,
            a.Sort));

    internal static PaymentTermKind ParseKind(string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        if (!Enum.TryParse<PaymentTermKind>(kind, ignoreCase: true, out var parsed))
            throw new PlatformRuleException($"Unknown payment term kind '{kind}'.");
        return parsed;
    }

    internal static ScheduleBasis ParseBasis(string basis)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basis);
        if (!Enum.TryParse<ScheduleBasis>(basis, ignoreCase: true, out var parsed))
            throw new PlatformRuleException($"Unknown schedule basis '{basis}'.");
        return parsed;
    }
}

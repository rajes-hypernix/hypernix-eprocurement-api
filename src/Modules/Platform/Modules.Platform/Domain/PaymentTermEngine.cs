namespace FSH.Modules.Platform.Domain;

/// <summary>Pure payment-term schedule calculator (no DI / clock / DB).</summary>
public static class PaymentTermEngine
{
    public sealed record ScheduleInstalment(
        DateOnly? DueDate,
        decimal Percent,
        DateOnly? DiscountDate,
        decimal? DiscountPct,
        string? Label);

    public static IReadOnlyList<ScheduleInstalment> ComputeSchedule(PaymentTerm term, DateOnly baseDate)
    {
        ArgumentNullException.ThrowIfNull(term);
        return term.Kind switch
        {
            PaymentTermKind.Net => [Net(term, baseDate)],
            PaymentTermKind.DateDriven => [DateDriven(term, baseDate)],
            PaymentTermKind.Schedule => Schedule(term, baseDate),
            _ => throw new PlatformRuleException($"Unknown payment term kind '{term.Kind}'."),
        };
    }

    public static DateOnly? ComputeDueDate(PaymentTerm term, DateOnly baseDate) =>
        ComputeSchedule(term, baseDate).Select(i => i.DueDate).FirstOrDefault(d => d is not null);

    private static ScheduleInstalment Net(PaymentTerm t, DateOnly baseDate)
    {
        if (t.DueDays is not { } dueDays)
            throw new PlatformRuleException($"Net payment term '{t.Code}' is missing DueDays.");
        var (discDate, discPct) = Discount(t, baseDate);
        return new ScheduleInstalment(baseDate.AddDays(dueDays), 100m, discDate, discPct, Label: null);
    }

    private static ScheduleInstalment DateDriven(PaymentTerm t, DateOnly baseDate)
    {
        if (t.DayOfMonth is not { } dom)
            throw new PlatformRuleException($"DateDriven payment term '{t.Code}' is missing DayOfMonth.");
        if (dom is < 1 or > 31)
            throw new PlatformRuleException($"DateDriven payment term '{t.Code}' has an out-of-range DayOfMonth ({dom}).");
        if (t.MonthsAhead is not { } monthsAhead || monthsAhead < 0)
            throw new PlatformRuleException($"DateDriven payment term '{t.Code}' is missing/invalid MonthsAhead.");

        var candidate = ClampedDate(baseDate, monthsAhead, dom);
        if (t.MinimumDaysBeforeDue is { } minDays && candidate < baseDate.AddDays(minDays))
            candidate = ClampedDate(baseDate, monthsAhead + 1, dom);

        var (discDate, discPct) = Discount(t, baseDate);
        return new ScheduleInstalment(candidate, 100m, discDate, discPct, Label: null);
    }

    private static DateOnly ClampedDate(DateOnly baseDate, int monthsAhead, int dayOfMonth)
    {
        var firstOfTarget = new DateOnly(baseDate.Year, baseDate.Month, 1).AddMonths(monthsAhead);
        var daysInMonth = DateTime.DaysInMonth(firstOfTarget.Year, firstOfTarget.Month);
        var day = Math.Min(dayOfMonth, daysInMonth);
        return new DateOnly(firstOfTarget.Year, firstOfTarget.Month, day);
    }

    private static (DateOnly? Date, decimal? Pct) Discount(PaymentTerm t, DateOnly baseDate) =>
        t is { DiscountPct: { } pct, DiscountDays: { } days }
            ? (baseDate.AddDays(days), pct)
            : (null, null);

    private static IReadOnlyList<ScheduleInstalment> Schedule(PaymentTerm t, DateOnly baseDate)
    {
        if (t.Rows.Count == 0)
            throw new PlatformRuleException($"Schedule payment term '{t.Code}' has no rows.");

        var result = new List<ScheduleInstalment>(t.Rows.Count);
        foreach (var row in t.Rows.OrderBy(r => r.Seq))
        {
            var dueDate = row.Basis switch
            {
                ScheduleBasis.Advance => baseDate,
                ScheduleBasis.DaysFromDoc => baseDate.AddDays(
                    row.Days ?? throw new PlatformRuleException(
                        $"Schedule row {row.Seq} of '{t.Code}' is DaysFromDoc but is missing Days.")),
                ScheduleBasis.MilestoneLabel => (DateOnly?)null,
                _ => throw new PlatformRuleException($"Schedule row {row.Seq} of '{t.Code}' has an unknown basis."),
            };
            result.Add(new ScheduleInstalment(dueDate, row.Percent, null, null, row.Label));
        }

        return result;
    }
}

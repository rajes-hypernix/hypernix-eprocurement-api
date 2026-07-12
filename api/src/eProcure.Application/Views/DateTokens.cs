using System.Globalization;
using System.Text.RegularExpressions;

namespace eProcure.Application.Views;

/// <summary>
/// The ruled date-token vocabulary in ONE place (extracted from SavedViewService at D7 so
/// entry-form defaults ride the SAME parser — no rival grammar): @today, @startOfMonth,
/// @endOfMonth, and the D5-ruled @today±Nd FORM. Returns the resolved DAY, or null when
/// the input is not a token; unknown @-strings are the caller's loud-fail to raise.
/// </summary>
public static partial class DateTokens
{
    [GeneratedRegex(@"^@today([+-]\d{1,4})d$")]
    private static partial Regex TodayOffset();

    public static DateTime? TryResolveDay(string raw, DateTime now)
    {
        DateTime? day = raw switch
        {
            "@today" => now.Date,
            "@startOfMonth" => new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            "@endOfMonth" => new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month), 0, 0, 0, DateTimeKind.Utc),
            _ => null,
        };
        if (day is null && TodayOffset().Match(raw) is { Success: true } offset)
            day = now.Date.AddDays(int.Parse(offset.Groups[1].Value, CultureInfo.InvariantCulture));
        return day;
    }
}

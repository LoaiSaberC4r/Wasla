using System.Globalization;

namespace Wasla.Infrastructure.Email;

internal sealed class EmailDateFormatter(string? timeZoneId)
{
    private static readonly CultureInfo Arabic = CultureInfo.GetCultureInfo("ar-EG");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    private readonly TimeZoneInfo zone = !string.IsNullOrWhiteSpace(timeZoneId) &&
        TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var resolved) ? resolved : TimeZoneInfo.Utc;

    internal string PlatformDateTime(DateTime utc, bool english)
    {
        // SQL timestamps are UTC even when the provider materializes them with an unspecified kind.
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
        var separator = english ? ", " : "، ";
        var zoneLabel = zone.Equals(TimeZoneInfo.Utc) ? " (UTC)" : "";
        return LocalDate(local, english) + separator + LocalTime(local, english) + zoneLabel;
    }

    internal static string LocalDate(DateTime local, bool english) => english
        ? local.ToString("d MMM yyyy", English)
        : local.ToString("d MMMM yyyy", Arabic);

    internal static string LocalTime(DateTime local, bool english)
    {
        if (english) return local.ToString("hh:mm tt", English);
        var period = local.Hour switch
        {
            0 when local.Minute == 0 => "منتصف الليل",
            0 => "بعد منتصف الليل",
            < 12 => "صباحًا",
            12 => "ظهرًا",
            _ => "مساءً"
        };
        return local.ToString("hh:mm", CultureInfo.InvariantCulture) + " " + period;
    }

    internal static string Remaining(TimeSpan duration, bool english)
    {
        if (duration <= TimeSpan.Zero) return english ? "Period ended" : "انتهت المدة";
        var days = duration.Days;
        if (days > 0) return Quantity(days, english, "day", "يوم واحد", "يومان", "أيام", "يومًا");
        var hours = duration.Hours;
        return hours > 0
            ? Quantity(hours, english, "hour", "ساعة واحدة", "ساعتان", "ساعات", "ساعة")
            : english ? "Less than an hour" : "أقل من ساعة";
    }

    private static string Quantity(int count, bool english, string unit, string one, string two, string few, string many)
    {
        var number = count.ToString(CultureInfo.InvariantCulture);
        if (english) return $"{number} {unit}{(count == 1 ? "" : "s")}";
        return count switch
        {
            1 => one,
            2 => two,
            _ => $"{number} {(count % 100 is >= 3 and <= 10 ? few : many)}"
        };
    }
}

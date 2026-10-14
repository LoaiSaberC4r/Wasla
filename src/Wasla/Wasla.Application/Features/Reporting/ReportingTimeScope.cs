using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Microsoft.Extensions.Options;
using Wasla.Domain.Resources;

namespace Wasla.Application.Features.Reporting;

public static class ReportingErrors
{
    public static Error InvalidFilter => Error.Validation("Report.InvalidFilter", ErrorMessage.GetString("Phase18InvalidFilter"));
    public static Error Denied => Error.Security("Report.AccessDenied", ErrorMessage.GetString("Phase18AccessDenied"));
    public static Error NotFound => Error.NotFound("Report.NotFound", ErrorMessage.GetString("Phase18NotFound"));
    public static Error Conflict => Error.Conflict("Phase18.ConcurrentModification", ErrorMessage.GetString("Phase18Conflict"));
    public static Error Invalid => Error.Validation("Phase18.Invalid", ErrorMessage.GetString("Phase18Invalid"));
}

public sealed class ReportingTimeScope(IDateTimeProvider clock, IOptions<Phase18Options> options) : IReportingTimeScope
{
    public Result<ReportingWindow> Resolve(ReportFilter filter)
    {
        var now = clock.UtcNow;
        var asOf = filter.AsOfUtc ?? now;
        if (asOf.Kind == DateTimeKind.Local || asOf > now || asOf == default ||
            filter.PageNumber is < 1 or > 1_000_000 || filter.PageSize is < 1 or > 100 ||
            filter.SortBy is not ("Date" or "Status" or "Reference" or "Amount") ||
            filter.SortDirection is not ("asc" or "desc") || !Enum.IsDefined(filter.Grouping) ||
            filter.ReservationStatus is { } rs && !Enum.IsDefined(rs) ||
            filter.TicketStatus is { } ts && !Enum.IsDefined(ts) ||
            filter.BookingSource is { } bs && !Enum.IsDefined(bs) ||
            filter.ExpenseCategory is { } category && !Enum.IsDefined(category))
            return Result<ReportingWindow>.Fail(ReportingErrors.InvalidFilter);
        var zoneId = filter.ReportTimeZoneId ?? options.Value.DefaultReportTimeZoneId;
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId); }
        catch (TimeZoneNotFoundException) { return Result<ReportingWindow>.Fail(ReportingErrors.InvalidFilter); }
        catch (InvalidTimeZoneException) { return Result<ReportingWindow>.Fail(ReportingErrors.InvalidFilter); }
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(asOf, DateTimeKind.Utc), zone));
        var to = filter.ToDate ?? today;
        var from = filter.FromDate ?? DateOnly.FromDayNumber(Math.Max(1, to.DayNumber - 29));
        return from > to || to.DayNumber - from.DayNumber >= 366 || from == DateOnly.MinValue || to == DateOnly.MaxValue
            ? Result<ReportingWindow>.Fail(ReportingErrors.InvalidFilter)
            : Result<ReportingWindow>.Ok(new(from, to, DateTime.SpecifyKind(asOf, DateTimeKind.Utc), now, zone.Id));
    }

    public static (DateTime FromUtc, DateTime BeforeUtc) Bounds(DateOnly from, DateOnly to, string zoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        return (DateBoundaryUtc(from, zone), DateBoundaryUtc(to.AddDays(1), zone));
    }

    public static DateTime DateBoundaryUtc(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);
        return zone.IsAmbiguousTime(local)
            ? new DateTimeOffset(local, zone.GetAmbiguousTimeOffsets(local).Max()).UtcDateTime
            : TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    public static decimal UnionMinutes(IEnumerable<(DateTime Start, DateTime End)> intervals)
    {
        var valid = intervals.Where(i => i.End > i.Start).OrderBy(i => i.Start).ThenBy(i => i.End).ToArray();
        if (valid.Length == 0) return 0;
        var start = valid[0].Start;
        var end = valid[0].End;
        decimal total = 0;
        foreach (var next in valid.Skip(1))
        {
            if (next.Start > end) { total += (decimal)(end - start).TotalMinutes; start = next.Start; end = next.End; }
            else if (next.End > end) end = next.End;
        }
        return total + (decimal)(end - start).TotalMinutes;
    }
}

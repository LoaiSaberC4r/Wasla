using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Common;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class ReportingQueryService
{
    private async Task<IReadOnlyList<ReportSeriesPoint>> PlatformCohortsAsync(ReportScope scope, ReportingWindow w, CancellationToken ct)
    {
        var bounds = ReportingTimeScope.Bounds(w.FromDate, w.ToDate, w.ReportTimeZoneId);
        var month = new DateOnly(w.FromDate.Year, w.FromDate.Month, 1);
        var monthsBefore = Math.Min(11, (month.Year - 1) * 12 + month.Month - 1);
        var cohortFrom = month.AddMonths(-monthsBefore);
        if (cohortFrom == DateOnly.MinValue) cohortFrom = cohortFrom.AddDays(1);
        var cohortBounds = ReportingTimeScope.Bounds(cohortFrom, w.ToDate, w.ReportTimeZoneId);
        var length = w.ToDate.DayNumber - w.FromDate.DayNumber + 1;
        var prior = ReportingTimeScope.Bounds(DateOnly.FromDayNumber(Math.Max(1, w.FromDate.DayNumber - length)), w.FromDate.AddDays(-1), w.ReportTimeZoneId);
        var bookingActivity = await db.AllReservations.AsNoTracking().Where(r => scope.PracticeIds.Contains(r.DoctorPracticeId) &&
            r.CreatedOnUtc >= prior.FromUtc && r.CreatedOnUtc < bounds.BeforeUtc && r.CreatedOnUtc <= w.AsOfUtc)
            .Select(r => new { r.DoctorId, r.PatientId, At = r.CreatedOnUtc }).ToArrayAsync(ct);
        var ticketActivity = await db.AllTickets.AsNoTracking().Where(t => scope.PracticeIds.Contains(t.DoctorPracticeId) &&
            t.CheckInTimeUtc >= prior.FromUtc && t.CheckInTimeUtc < bounds.BeforeUtc && t.CheckInTimeUtc <= w.AsOfUtc)
            .Select(t => new { t.DoctorId, t.PatientId, At = t.CheckInTimeUtc }).ToArrayAsync(ct);
        var activity = bookingActivity.Concat(ticketActivity).ToArray();
        var doctors = await db.Doctors.AsNoTracking().Where(d => (scope.DoctorId == null || d.Id == scope.DoctorId) &&
            d.CreatedOnUtc >= cohortBounds.FromUtc && d.CreatedOnUtc < bounds.BeforeUtc && d.CreatedOnUtc <= w.AsOfUtc)
            .Select(d => new { d.Id, d.CreatedOnUtc }).ToArrayAsync(ct);
        var patients = await db.Patients.AsNoTracking().Where(p => p.CreatedOnUtc >= cohortBounds.FromUtc && p.CreatedOnUtc < bounds.BeforeUtc && p.CreatedOnUtc <= w.AsOfUtc)
            .Select(p => new { p.Id, p.CreatedOnUtc }).ToArrayAsync(ct);
        List<ReportSeriesPoint> result = [];
        string Month(DateTime at) => OperationalKpiCalculator.LocalDate(at, w.ReportTimeZoneId).ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
        var beforeDoctors = activity.Where(a => a.At < bounds.FromUtc).Select(a => a.DoctorId).ToHashSet();
        var currentDoctors = activity.Where(a => a.At >= bounds.FromUtc).Select(a => a.DoctorId).ToHashSet();
        foreach (var cohort in doctors.GroupBy(d => Month(d.CreatedOnUtc)))
        {
            var ids = cohort.Select(d => d.Id).ToHashSet();
            result.Add(new("DoctorRegistrationCohort:" + cohort.Key, "دفعة تسجيل الأطباء " + cohort.Key, "Doctor registration cohort " + cohort.Key,
                [calculator.Count("DoctorAcquisition", ids.Count), calculator.Ratio("DoctorRetention", ids.Intersect(beforeDoctors).Intersect(currentDoctors).Count(), ids.Intersect(beforeDoctors).Count())]));
        }
        var beforePatients = activity.Where(a => a.At < bounds.FromUtc).Select(a => a.PatientId).ToHashSet();
        var currentPatients = activity.Where(a => a.At >= bounds.FromUtc).Select(a => a.PatientId).ToHashSet();
        var patientCohorts = patients.GroupBy(p => Month(p.CreatedOnUtc)).Select(g => new { Key = g.Key, Ids = g.Select(p => p.Id).ToHashSet() }).ToArray();
        // Suppress the whole related cohort family when any cell could identify a small patient group.
        bool Small(int n) => n is > 0 and < 5;
        var suppress = patientCohorts.Any(c => Small(c.Ids.Count) || Small(c.Ids.Intersect(beforePatients).Count()) ||
            Small(c.Ids.Intersect(beforePatients).Intersect(currentPatients).Count()));
        foreach (var cohort in patientCohorts)
            result.Add(new("PatientRegistrationCohort:" + cohort.Key, "دفعة تسجيل المرضى " + cohort.Key, "Patient registration cohort " + cohort.Key,
                suppress ? [calculator.Missing("PatientAcquisition", ReportDataStatus.NotAvailable, "Report.SmallGroupAndComplementarySuppression"),
                    calculator.Missing("PatientRetention", ReportDataStatus.NotAvailable, "Report.SmallGroupAndComplementarySuppression")]
                    : [calculator.Count("PatientAcquisition", cohort.Ids.Count),
                        calculator.Ratio("PatientRetention", cohort.Ids.Intersect(beforePatients).Intersect(currentPatients).Count(), cohort.Ids.Intersect(beforePatients).Count())]));
        return result;
    }
}

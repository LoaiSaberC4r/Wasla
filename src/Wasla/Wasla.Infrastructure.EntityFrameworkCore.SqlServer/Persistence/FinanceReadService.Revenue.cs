using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Finance;
using Wasla.Domain.Doctors;
using Wasla.Domain.Payments;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class FinanceReadService
{
    public async Task<Result<DoctorRevenueDashboard>> GetDoctorDashboardAsync(
        DateOnly fromDate, DateOnly toDate, Guid? practiceId, CancellationToken cancellationToken)
    {
        var doctor = await ResolveDoctorAsync(PermissionNames.DoctorRevenueViewOwn, cancellationToken);
        if (doctor.IsFailure) return Result<DoctorRevenueDashboard>.Fail(doctor.Errors);
        if (!ValidRange(fromDate, toDate)) return Result<DoctorRevenueDashboard>.Fail(InvalidFilter);
        if (practiceId is { } targetPractice && !await OwnsPracticeAsync(doctor.Value, targetPractice, cancellationToken))
            return Result<DoctorRevenueDashboard>.Fail(AccessDenied);

        var movements = RevenueMovements(fromDate, toDate, doctor.Value, practiceId);
        var byPracticeData = await movements.GroupBy(item => item.PracticeId)
            .Select(group => new RevenueGroup(group.Key, group.Sum(item => item.GrossRevenue),
                group.Sum(item => item.Refunds), group.Sum(item => item.PaymentCount),
                group.Sum(item => item.RefundCount))).ToArrayAsync(cancellationToken);
        var practiceIds = byPracticeData.Select(item => item.Id).ToArray();
        var practiceNames = await db.DoctorPractices.AsNoTracking()
            .Where(item => practiceIds.Contains(item.Id))
            .Select(item => new { item.Id, item.NameAr, item.NameEn })
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var byPractice = byPracticeData.Select(item => new PracticeRevenuePoint(
            item.Id, practiceNames[item.Id].NameAr, practiceNames[item.Id].NameEn,
            item.GrossRevenue, item.Refunds, item.GrossRevenue - item.Refunds,
            item.PaymentCount, item.RefundCount)).OrderBy(item => item.PracticeNameAr).ToArray();
        var summary = Summary(byPracticeData);

        var daily = await movements.GroupBy(item => item.BusinessDate)
            .Select(group => new { Date = group.Key, Gross = group.Sum(item => item.GrossRevenue),
                Refunds = group.Sum(item => item.Refunds) })
            .OrderBy(item => item.Date).ToArrayAsync(cancellationToken);
        var dailyTrend = daily.Select(item => new DailyRevenuePoint(
            item.Date, item.Gross, item.Refunds, item.Gross - item.Refunds)).ToArray();

        var paymentMethods = await db.Payments.AsNoTracking()
            .Where(item => item.DoctorId == doctor.Value &&
                           (!practiceId.HasValue || item.DoctorPracticeId == practiceId.Value) &&
                           item.BusinessDate >= fromDate && item.BusinessDate <= toDate)
            .GroupBy(item => item.PaymentMethod)
            .Select(group => new MethodDistributionPoint(group.Key, group.Sum(item => item.Amount), group.Count()))
            .ToArrayAsync(cancellationToken);
        var refundMethods = await db.Refunds.AsNoTracking()
            .Where(item => item.DoctorId == doctor.Value &&
                           (!practiceId.HasValue || item.DoctorPracticeId == practiceId.Value) &&
                           item.BusinessDate >= fromDate && item.BusinessDate <= toDate)
            .GroupBy(item => item.RefundMethod)
            .Select(group => new MethodDistributionPoint(group.Key, group.Sum(item => item.Amount), group.Count()))
            .ToArrayAsync(cancellationToken);

        var segmentGroups = await movements.GroupBy(item => new
            { item.SegmentId, item.SegmentNameAr, item.SegmentNameEn })
            .Select(group => new
            {
                group.Key.SegmentId, group.Key.SegmentNameAr, group.Key.SegmentNameEn,
                Gross = group.Sum(item => item.GrossRevenue),
                Refunds = group.Sum(item => item.Refunds),
                Payments = group.Sum(item => item.PaymentCount),
                RefundCount = group.Sum(item => item.RefundCount)
            }).ToArrayAsync(cancellationToken);
        var bySegment = segmentGroups.GroupBy(item => item.SegmentId)
            .Select(group => new SegmentRevenuePoint(group.Key,
                group.First().SegmentNameAr, group.First().SegmentNameEn,
                group.Sum(item => item.Gross), group.Sum(item => item.Refunds),
                group.Sum(item => item.Gross - item.Refunds),
                group.Sum(item => item.Payments), group.Sum(item => item.RefundCount)))
            .OrderBy(item => item.SegmentNameAr).ToArray();

        var visitGroups = await movements.GroupBy(item => new
            { item.VisitTypeId, item.VisitTypeCode, item.VisitTypeNameAr, item.VisitTypeNameEn })
            .Select(group => new
            {
                group.Key.VisitTypeId, group.Key.VisitTypeCode, group.Key.VisitTypeNameAr,
                group.Key.VisitTypeNameEn,
                Gross = group.Sum(item => item.GrossRevenue),
                Refunds = group.Sum(item => item.Refunds),
                Payments = group.Sum(item => item.PaymentCount),
                RefundCount = group.Sum(item => item.RefundCount)
            }).ToArrayAsync(cancellationToken);
        var byVisitType = visitGroups.GroupBy(item => item.VisitTypeId)
            .Select(group => new VisitTypeRevenuePoint(group.Key,
                group.First().VisitTypeCode, group.First().VisitTypeNameAr, group.First().VisitTypeNameEn,
                group.Sum(item => item.Gross), group.Sum(item => item.Refunds),
                group.Sum(item => item.Gross - item.Refunds),
                group.Sum(item => item.Payments), group.Sum(item => item.RefundCount)))
            .OrderBy(item => item.VisitTypeNameAr).ToArray();

        return Result<DoctorRevenueDashboard>.Ok(new(summary, dailyTrend, byPractice,
            paymentMethods.OrderBy(item => item.Method).ToArray(),
            refundMethods.OrderBy(item => item.Method).ToArray(), bySegment, byVisitType));
    }

    public async Task<Result<PlatformRevenueDashboard>> GetPlatformDashboardAsync(
        DateOnly fromDate, DateOnly toDate, Guid? doctorId, Guid? practiceId, CancellationToken cancellationToken)
    {
        var access = await AccessSnapshotAsync(cancellationToken);
        if (access.IsFailure || !access.Value.Roles.Contains(SystemRoleNames.SuperAdmin) ||
            !access.Value.Permissions.Contains(PermissionNames.PlatformRevenueViewAggregates))
            return Result<PlatformRevenueDashboard>.Fail(AccessDenied);
        if (!ValidRange(fromDate, toDate)) return Result<PlatformRevenueDashboard>.Fail(InvalidFilter);
        if (practiceId is { } targetPractice && !await db.DoctorPractices.AsNoTracking().AnyAsync(
                item => item.Id == targetPractice && (!doctorId.HasValue || item.DoctorId == doctorId.Value),
                cancellationToken))
            return Result<PlatformRevenueDashboard>.Fail(InvalidFilter);

        var groups = await RevenueMovements(fromDate, toDate, doctorId, practiceId)
            .GroupBy(item => new { item.DoctorId, item.PracticeId })
            .Select(group => new
            {
                group.Key.DoctorId, group.Key.PracticeId,
                Gross = group.Sum(item => item.GrossRevenue),
                Refunds = group.Sum(item => item.Refunds),
                Payments = group.Sum(item => item.PaymentCount),
                RefundCount = group.Sum(item => item.RefundCount)
            }).ToArrayAsync(cancellationToken);
        var doctorIds = groups.Select(item => item.DoctorId).Distinct().ToArray();
        var practiceIds = groups.Select(item => item.PracticeId).Distinct().ToArray();
        var doctors = await db.Doctors.AsNoTracking().Where(item => doctorIds.Contains(item.Id))
            .Select(item => new { item.Id, item.NameAr, item.NameEn })
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var practices = await db.DoctorPractices.AsNoTracking().Where(item => practiceIds.Contains(item.Id))
            .Select(item => new { item.Id, item.NameAr, item.NameEn })
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var rows = groups.Select(item => new PlatformRevenuePoint(item.DoctorId,
            doctors[item.DoctorId].NameAr, doctors[item.DoctorId].NameEn,
            item.PracticeId, practices[item.PracticeId].NameAr, practices[item.PracticeId].NameEn,
            item.Gross, item.Refunds, item.Gross - item.Refunds, item.Payments, item.RefundCount))
            .OrderBy(item => item.DoctorNameAr).ThenBy(item => item.PracticeNameAr).ToArray();
        var summary = new RevenueSummary(groups.Sum(item => item.Gross), groups.Sum(item => item.Refunds),
            groups.Sum(item => item.Gross - item.Refunds), groups.Sum(item => item.Payments),
            groups.Sum(item => item.RefundCount), "EGP");
        return Result<PlatformRevenueDashboard>.Ok(new(summary, rows));
    }

    private IQueryable<RevenueMovement> RevenueMovements(
        DateOnly fromDate, DateOnly toDate, Guid? doctorId, Guid? practiceId)
    {
        var payments = from payment in db.Payments.AsNoTracking()
            join ticket in db.Tickets.AsNoTracking() on payment.TicketId equals ticket.Id
            where payment.BusinessDate >= fromDate && payment.BusinessDate <= toDate &&
                  (!doctorId.HasValue || payment.DoctorId == doctorId.Value) &&
                  (!practiceId.HasValue || payment.DoctorPracticeId == practiceId.Value)
            select new RevenueMovement
            {
                DoctorId = payment.DoctorId,
                PracticeId = payment.DoctorPracticeId,
                BusinessDate = payment.BusinessDate,
                SegmentId = ticket.SegmentId,
                SegmentNameAr = ticket.SegmentNameArSnapshot,
                SegmentNameEn = ticket.SegmentNameEnSnapshot,
                VisitTypeId = ticket.VisitTypeId,
                VisitTypeCode = ticket.VisitTypeCodeSnapshot,
                VisitTypeNameAr = ticket.VisitTypeNameArSnapshot,
                VisitTypeNameEn = ticket.VisitTypeNameEnSnapshot,
                GrossRevenue = payment.Amount,
                Refunds = 0m,
                PaymentCount = 1,
                RefundCount = 0
            };
        var refunds = from refund in db.Refunds.AsNoTracking()
            join ticket in db.Tickets.AsNoTracking() on refund.TicketId equals ticket.Id
            where refund.BusinessDate >= fromDate && refund.BusinessDate <= toDate &&
                  (!doctorId.HasValue || refund.DoctorId == doctorId.Value) &&
                  (!practiceId.HasValue || refund.DoctorPracticeId == practiceId.Value)
            select new RevenueMovement
            {
                DoctorId = refund.DoctorId,
                PracticeId = refund.DoctorPracticeId,
                BusinessDate = refund.BusinessDate,
                SegmentId = ticket.SegmentId,
                SegmentNameAr = ticket.SegmentNameArSnapshot,
                SegmentNameEn = ticket.SegmentNameEnSnapshot,
                VisitTypeId = ticket.VisitTypeId,
                VisitTypeCode = ticket.VisitTypeCodeSnapshot,
                VisitTypeNameAr = ticket.VisitTypeNameArSnapshot,
                VisitTypeNameEn = ticket.VisitTypeNameEnSnapshot,
                GrossRevenue = 0m,
                Refunds = refund.Amount,
                PaymentCount = 0,
                RefundCount = 1
            };
        return payments.Concat(refunds);
    }

    private static RevenueSummary Summary(IEnumerable<RevenueGroup> groups)
    {
        var rows = groups.ToArray();
        var gross = rows.Sum(item => item.GrossRevenue);
        var refunds = rows.Sum(item => item.Refunds);
        return new(gross, refunds, gross - refunds, rows.Sum(item => item.PaymentCount),
            rows.Sum(item => item.RefundCount), "EGP");
    }

    private static bool ValidRange(DateOnly fromDate, DateOnly toDate)
        => fromDate != default && toDate != default && fromDate <= toDate;

    private sealed record RevenueGroup(Guid Id, decimal GrossRevenue, decimal Refunds,
        int PaymentCount, int RefundCount);

    private sealed class RevenueMovement
    {
        public Guid DoctorId { get; init; }
        public Guid PracticeId { get; init; }
        public DateOnly BusinessDate { get; init; }
        public Guid SegmentId { get; init; }
        public string SegmentNameAr { get; init; } = string.Empty;
        public string? SegmentNameEn { get; init; }
        public Guid VisitTypeId { get; init; }
        public string VisitTypeCode { get; init; } = string.Empty;
        public string VisitTypeNameAr { get; init; } = string.Empty;
        public string? VisitTypeNameEn { get; init; }
        public decimal GrossRevenue { get; init; }
        public decimal Refunds { get; init; }
        public int PaymentCount { get; init; }
        public int RefundCount { get; init; }
    }
}

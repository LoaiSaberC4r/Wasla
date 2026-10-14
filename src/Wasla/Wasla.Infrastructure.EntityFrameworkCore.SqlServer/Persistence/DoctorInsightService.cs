using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Reporting;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Domain.OperatingFinance;
using Wasla.Domain.Payments;
using Wasla.Domain.Reporting;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class DoctorInsightService(WaslaDbContext db, IReportingAccessGuard guard, IInsightEvidenceReader evidence,
    ITicketQueueLock coordination, IDateTimeProvider clock) : IDoctorInsightService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] SignalCodes = ["WaitToFirstCall", "WaitToServiceStart", "ReservationNoShowRate", "CancellationRateScheduledCohort", "ReservationToTicketConversion"];

    public async Task<Result<ReportPage<DoctorInsightResponse>>> ListAsync(Guid? practiceId, int pageNumber, int pageSize, CancellationToken ct, DoctorInsightState? state = null)
    {
        var scope = await guard.AuthorizeAsync(ReportActor.Doctor, Phase18PermissionNames.InsightsView, practiceId, null, ct);
        if (scope.IsFailure) return Result<ReportPage<DoctorInsightResponse>>.Fail(scope.Errors);
        if (pageNumber is < 1 or > 1_000_000 || pageSize is < 1 or > 100 || state is { } selected && !Enum.IsDefined(selected)) return Result<ReportPage<DoctorInsightResponse>>.Fail(ReportingErrors.InvalidFilter);
        var computed = await ComputeAsync(practiceId, scope.Value, ct);
        if (computed.IsFailure) return Result<ReportPage<DoctorInsightResponse>>.Fail(computed.Errors);
        var query = db.Set<DoctorInsightFollowUp>().AsNoTracking().Where(i => i.DoctorId == scope.Value.DoctorId && scope.Value.PracticeIds.Contains(i.PracticeId));
        var existingIds = await query.Select(i => i.Id).ToArrayAsync(ct);
        var newSignals = computed.Value.Where(i => !existingIds.Contains(i.InsightId) && (state == null || state == DoctorInsightState.New)).ToArray();
        if (state != null) query = query.Where(i => i.State == state);
        var count = await query.LongCountAsync(ct) + newSignals.Length;
        var skip = (pageNumber - 1) * pageSize;
        var newPage = newSignals.OrderByDescending(i => i.CurrentWindow.ToDate).ThenBy(i => i.InsightId).Skip(skip).Take(pageSize).ToList();
        var persistedSkip = Math.Max(0, skip - newSignals.Length);
        var persisted = await query.OrderByDescending(i => i.CreatedOnUtc).ThenBy(i => i.Id).Skip(persistedSkip).Take(pageSize - newPage.Count).ToArrayAsync(ct);
        newPage.AddRange(persisted.Select(i => Response(i, scope.Value)));
        return Result<ReportPage<DoctorInsightResponse>>.Ok(new(newPage, count, pageNumber, pageSize));
    }
    public async Task<Result<DoctorInsightResponse>> DetailAsync(Guid id, CancellationToken ct)
    {
        var scope = await guard.AuthorizeAsync(ReportActor.Doctor, Phase18PermissionNames.InsightsView, null, null, ct);
        if (scope.IsFailure) return Result<DoctorInsightResponse>.Fail(scope.Errors);
        var row = await db.Set<DoctorInsightFollowUp>().AsNoTracking().SingleOrDefaultAsync(i => i.Id == id && i.DoctorId == scope.Value.DoctorId, ct);
        if (row != null) return Result<DoctorInsightResponse>.Ok(Response(row, scope.Value));
        var computed = await ComputeAsync(null, scope.Value, ct);
        if (computed.IsFailure) return Result<DoctorInsightResponse>.Fail(computed.Errors);
        var match = computed.Value.SingleOrDefault(i => i.InsightId == id);
        return match is null ? Result<DoctorInsightResponse>.Fail(ReportingErrors.NotFound) : Result<DoctorInsightResponse>.Ok(match);
    }
    public async Task<Result<ReportPage<DoctorInsightHistoryResponse>>> HistoryAsync(Guid id, int pageNumber, int pageSize, CancellationToken ct)
    {
        if (pageNumber is < 1 or > 1_000_000 || pageSize is < 1 or > 100) return Result<ReportPage<DoctorInsightHistoryResponse>>.Fail(ReportingErrors.InvalidFilter);
        var detail = await DetailAsync(id, ct);
        if (detail.IsFailure) return Result<ReportPage<DoctorInsightHistoryResponse>>.Fail(detail.Errors);
        var query = db.Set<DoctorInsightHistory>().AsNoTracking().Where(h => h.InsightId == id);
        var count = await query.LongCountAsync(ct);
        var rows = await query.OrderBy(h => h.SequenceNumber).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(h => new DoctorInsightHistoryResponse(h.Id, h.Operation, h.State, h.ActorApplicationUserId, h.RecordedAtUtc, h.Reason, h.SequenceNumber)).ToArrayAsync(ct);
        return Result<ReportPage<DoctorInsightHistoryResponse>>.Ok(new(rows, count, pageNumber, pageSize));
    }
    public async Task<Result<DoctorInsightResponse>> TrackAsync(TrackDoctorInsightCommand c, CancellationToken ct)
    {
        var access = await guard.AuthorizeAsync(ReportActor.Doctor, Phase18PermissionNames.InsightsTrack, null, null, ct);
        if (access.IsFailure) return Result<DoctorInsightResponse>.Fail(access.Errors);
        var view = await guard.AuthorizeAsync(ReportActor.Doctor, Phase18PermissionNames.InsightsView, null, null, ct);
        if (view.IsFailure) return Result<DoctorInsightResponse>.Fail(view.Errors);
        if (string.IsNullOrWhiteSpace(c.IdempotencyKey) || c.IdempotencyKey.Length > 200) return Result<DoctorInsightResponse>.Fail(OperatingFinanceErrors.Invalid("Insight.IdempotencyKeyRequired"));
        await coordination.AcquireIdempotencyAsync(Guid.Empty, "DoctorInsight", c.InsightId.ToString("N"), ct);
        var operation = "P18.Insight." + c.TargetState;
        var key = c.IdempotencyKey.Trim();
        await coordination.AcquireIdempotencyAsync(access.Value.ActorId, operation, key, ct);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(c with { IdempotencyKey = string.Empty }))));
        var idem = await db.FinancialIdempotencyRecords.SingleOrDefaultAsync(r => r.ActorApplicationUserId == access.Value.ActorId && r.Operation == operation && r.IdempotencyKey == key, ct);
        if (idem != null)
            return idem.RequestFingerprint == fingerprint ? await DetailAsync(c.InsightId, ct) : Result<DoctorInsightResponse>.Fail(OperatingFinanceErrors.State("Insight.IdempotencyKeyReused"));
        var row = await db.Set<DoctorInsightFollowUp>().Include(i => i.History).SingleOrDefaultAsync(i => i.Id == c.InsightId && i.DoctorId == access.Value.DoctorId, ct);
        if (row is null)
        {
            if (c.TargetState is not (DoctorInsightState.Acknowledged or DoctorInsightState.Dismissed)) return Result<DoctorInsightResponse>.Fail(ReportingErrors.NotFound);
            var detail = await DetailAsync(c.InsightId, ct);
            if (detail.IsFailure) return detail;
            row = new(c.InsightId, access.Value.DoctorId!.Value, detail.Value.PracticeId, detail.Value.MetricCode,
                JsonSerializer.Serialize(detail.Value, JsonOptions), access.Value.ActorId, clock.UtcNow);
            db.Set<DoctorInsightFollowUp>().Add(row);
        }
        else
        {
            byte[] token;
            try { token = Convert.FromBase64String(c.Request.RowVersion ?? string.Empty); } catch (FormatException) { return Result<DoctorInsightResponse>.Fail(ReportingErrors.Conflict); }
            if (!token.AsSpan().SequenceEqual(row.RowVersion)) return Result<DoctorInsightResponse>.Fail(ReportingErrors.Conflict);
            db.Entry(row).Property(i => i.RowVersion).OriginalValue = token;
        }
        var changed = row.Transition(c.TargetState, c.Request.Reason, access.Value.ActorId, clock.UtcNow);
        if (changed.IsFailure) return Result<DoctorInsightResponse>.Fail(changed.Errors);
        db.Set<DoctorInsightHistory>().Add(row.History.Last());
        idem = new(Guid.NewGuid(), access.Value.ActorId, operation, key, fingerprint, clock.UtcNow);
        idem.Complete(row.Id, clock.UtcNow); db.FinancialIdempotencyRecords.Add(idem);
        await db.SaveChangesAsync(ct);
        return Result<DoctorInsightResponse>.Ok(Response(row, access.Value));
    }
    private async Task<Result<IReadOnlyList<DoctorInsightResponse>>> ComputeAsync(Guid? practiceId, ReportScope scope, CancellationToken ct)
    {
        var result = await evidence.ReadComparisonsAsync(practiceId, ct);
        if (result.IsFailure) return Result<IReadOnlyList<DoctorInsightResponse>>.Fail(result.Errors);
        List<DoctorInsightResponse> signals = [];
        foreach (var practice in result.Value)
        foreach (var code in SignalCodes)
        {
            var current = practice.CurrentMetrics.Single(m => m.MetricCode == code);
            var previous = practice.PreviousMetrics.Single(m => m.MetricCode == code);
            if (current.DataStatus != ReportDataStatus.Complete || previous.DataStatus != ReportDataStatus.Complete ||
                current.EligibleSampleCount < 20 || previous.EligibleSampleCount < 20 || current.Denominator <= 0 || previous.Denominator <= 0 ||
                current.Value is not { } now || previous.Value is not { } before || before <= 0) continue;
            var change = code == "ReservationToTicketConversion" ? (before - now) / before : (now - before) / before;
            if (change < 0.25m) continue;
            var identity = Encoding.UTF8.GetBytes($"{scope.DoctorId:N}|{practice.PracticeId:N}|{code}|{practice.CurrentWindow.FromDate:yyyyMMdd}|{practice.CurrentWindow.ToDate:yyyyMMdd}");
            var id = new Guid(SHA256.HashData(identity).AsSpan(0, 16));
            var titleAr = "تغير تشغيلي في " + current.NameAr; var titleEn = "Operational change: " + current.NameEn;
            var en = string.Create(CultureInfo.InvariantCulture, $"Current {now:0.##}; preceding period {before:0.##}; relative deterioration {change:P0}. Based on valid recorded operational events.");
            var ar = string.Create(CultureInfo.InvariantCulture, $"القيمة الحالية {now:0.##} مقابل {before:0.##} للفترة السابقة. تدهور نسبي {change:P0} استنادًا إلى أحداث تشغيلية مسجلة وصالحة.");
            signals.Add(new(id, practice.PracticeId, code, titleAr, titleEn, ar, en, practice.CurrentWindow, practice.PreviousWindow,
                now, before, current.Numerator!.Value, current.Denominator!.Value, previous.Numerator!.Value, previous.Denominator!.Value,
                current.EligibleSampleCount, practice.AsOfUtc, ar, en, ReportDataStatus.Complete,
                "راجع مسار التشغيل وسجل المتابعة المناسبة.", "Review the operational workflow and record an appropriate follow-up.",
                DoctorInsightState.New, false, null, Actions(DoctorInsightState.New, scope)));
        }
        return Result<IReadOnlyList<DoctorInsightResponse>>.Ok(signals);
    }
    private static DoctorInsightResponse Response(DoctorInsightFollowUp row, ReportScope scope)
        => (JsonSerializer.Deserialize<DoctorInsightResponse>(row.EvidenceJson, JsonOptions)
            ?? throw new InvalidOperationException("Insight evidence is unavailable.")) with
        { State = row.State, IsPersisted = true, RowVersion = Convert.ToBase64String(row.RowVersion), AvailableActions = Actions(row.State, scope) };
    private static ReportAction[] Actions(DoctorInsightState state, ReportScope scope)
    {
        var allowed = scope.Permissions.Contains(Phase18PermissionNames.InsightsTrack);
        return [new("Acknowledge", allowed && state == DoctorInsightState.New), new("Dismiss", allowed && state == DoctorInsightState.New),
            new("Resolve", allowed && state == DoctorInsightState.Acknowledged)];
    }
}

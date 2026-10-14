using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Operations;
using Wasla.Application.Features.Reporting;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Domain.OperatingFinance;
using Wasla.Domain.Operations;
using Wasla.Domain.Payments;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class HandoverService(WaslaDbContext db, IReportingAccessGuard guard, IReportingTimeScope time,
    ITicketQueueLock coordination, IDateTimeProvider clock) : IHandoverService
{
    public async Task<Result<ReportPage<HandoverResponse>>> ListAsync(ReportActor actor, HandoverFilter f, CancellationToken ct)
    {
        var access = await guard.AuthorizeAsync(actor, Permission(actor, HandoverOperation.Create, view: true), f.PracticeId, null, ct);
        if (access.IsFailure) return Result<ReportPage<HandoverResponse>>.Fail(access.Errors);
        var window = time.Resolve(new(f.FromDate, f.ToDate, PageNumber: f.PageNumber, PageSize: f.PageSize, SortDirection: f.SortDirection));
        if (window.IsFailure || f.Status is { } status && !Enum.IsDefined(status)) return Result<ReportPage<HandoverResponse>>.Fail(ReportingErrors.InvalidFilter);
        var bounds = ReportingTimeScope.Bounds(window.Value.FromDate, window.Value.ToDate, window.Value.ReportTimeZoneId);
        var query = db.Set<ReceptionHandover>().AsNoTracking().Where(h => access.Value.PracticeIds.Contains(h.DoctorPracticeId) &&
            (f.Status == null || h.Status == f.Status) && h.CreatedOnUtc >= bounds.FromUtc && h.CreatedOnUtc < bounds.BeforeUtc);
        var count = await query.LongCountAsync(ct);
        var rows = await (f.SortDirection == "asc" ? query.OrderBy(h => h.CreatedOnUtc) : query.OrderByDescending(h => h.CreatedOnUtc))
            .ThenBy(h => h.Id).Skip((f.PageNumber - 1) * f.PageSize).Take(f.PageSize).ToArrayAsync(ct);
        var permissions = await DelegatedPermissionsAsync(access.Value, ct);
        return Result<ReportPage<HandoverResponse>>.Ok(new(rows.Select(h => Response(h, access.Value, permissions)).ToArray(), count, f.PageNumber, f.PageSize));
    }
    public async Task<Result<HandoverResponse>> DetailAsync(ReportActor actor, Guid practiceId, Guid id, CancellationToken ct)
    {
        var access = await guard.AuthorizeAsync(actor, Permission(actor, HandoverOperation.Create, true), practiceId, null, ct);
        if (access.IsFailure) return Result<HandoverResponse>.Fail(access.Errors);
        var row = await db.Set<ReceptionHandover>().AsNoTracking().SingleOrDefaultAsync(h => h.Id == id && h.DoctorPracticeId == practiceId, ct);
        return row is null ? Result<HandoverResponse>.Fail(ReportingErrors.NotFound)
            : Result<HandoverResponse>.Ok(Response(row, access.Value, await DelegatedPermissionsAsync(access.Value, ct)));
    }
    public async Task<Result<ReportPage<HandoverHistoryResponse>>> HistoryAsync(ReportActor actor, Guid practiceId, Guid id,
        int pageNumber, int pageSize, CancellationToken ct)
    {
        var detail = await DetailAsync(actor, practiceId, id, ct);
        if (detail.IsFailure) return Result<ReportPage<HandoverHistoryResponse>>.Fail(detail.Errors);
        if (pageNumber is < 1 or > 1_000_000 || pageSize is < 1 or > 100) return Result<ReportPage<HandoverHistoryResponse>>.Fail(ReportingErrors.InvalidFilter);
        var query = db.Set<ReceptionHandoverHistory>().AsNoTracking().Where(h => h.HandoverId == id);
        var count = await query.LongCountAsync(ct);
        var rows = await query.OrderBy(h => h.SequenceNumber).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(h => new HandoverHistoryResponse(h.Id, h.Operation, h.Status, h.ActorApplicationUserId, h.RecordedAtUtc, h.Note, h.SequenceNumber)).ToArrayAsync(ct);
        return Result<ReportPage<HandoverHistoryResponse>>.Ok(new(rows, count, pageNumber, pageSize));
    }
    public async Task<Result<HandoverResponse>> WriteAsync(WriteHandoverCommand c, CancellationToken ct)
    {
        if (c.Actor is not (ReportActor.Doctor or ReportActor.Reception) || !Enum.IsDefined(c.Operation) ||
            string.IsNullOrWhiteSpace(c.IdempotencyKey) || c.IdempotencyKey.Length > 200)
            return Result<HandoverResponse>.Fail(OperatingFinanceErrors.Invalid());
        var access = await guard.AuthorizeAsync(c.Actor, Permission(c.Actor, c.Operation), c.PracticeId, null, ct);
        if (access.IsFailure) return Result<HandoverResponse>.Fail(access.Errors);
        var view = await guard.AuthorizeAsync(c.Actor, Permission(c.Actor, c.Operation, true), c.PracticeId, null, ct);
        if (view.IsFailure) return Result<HandoverResponse>.Fail(view.Errors);
        var scope = access.Value;
        var operation = "P18.Handover." + c.Operation;
        var key = c.IdempotencyKey.Trim();
        if (c.HandoverId is { } target) await coordination.AcquireIdempotencyAsync(Guid.Empty, "Handover", target.ToString("N"), ct);
        await coordination.AcquireIdempotencyAsync(scope.ActorId, operation, key, ct);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(c with { IdempotencyKey = string.Empty }))));
        var idem = await db.FinancialIdempotencyRecords.SingleOrDefaultAsync(r => r.ActorApplicationUserId == scope.ActorId && r.Operation == operation && r.IdempotencyKey == key, ct);
        if (idem != null)
            return idem.RequestFingerprint == fingerprint && idem.ResultId is { } existing ? await DetailAsync(c.Actor, c.PracticeId, existing, ct)
                : Result<HandoverResponse>.Fail(OperatingFinanceErrors.State("Handover.IdempotencyKeyReused"));
        ReceptionHandover row;
        if (c.Operation == HandoverOperation.Create && c.Create is { } create)
        {
            if (create.ReservationId is { } reservation && !await db.Reservations.AnyAsync(r => r.Id == reservation && r.DoctorPracticeId == c.PracticeId, ct) ||
                create.TicketId is { } ticket && !await db.Tickets.AnyAsync(t => t.Id == ticket && t.DoctorPracticeId == c.PracticeId, ct) ||
                create.PreviousHandoverId is { } previous && !await db.Set<ReceptionHandover>().AnyAsync(h => h.Id == previous && h.DoctorPracticeId == c.PracticeId, ct))
                return Result<HandoverResponse>.Fail(ReportingErrors.NotFound);
            if (create.ReservationId != null && create.TicketId != null && !await db.Tickets.AnyAsync(t => t.Id == create.TicketId && t.ReservationId == create.ReservationId, ct))
                return Result<HandoverResponse>.Fail(OperatingFinanceErrors.Invalid("Handover.LinkedResourcesMismatch"));
            var doctorId = await db.DoctorPractices.Where(p => p.Id == c.PracticeId).Select(p => p.DoctorId).SingleAsync(ct);
            var result = ReceptionHandover.Create(Guid.NewGuid(), doctorId, c.PracticeId, create.Summary, create.ReservationId,
                create.TicketId, create.PreviousHandoverId, scope.ActorId, clock.UtcNow);
            if (result.IsFailure) return Result<HandoverResponse>.Fail(result.Errors);
            row = result.Value; db.Set<ReceptionHandover>().Add(row);
        }
        else
        {
            var existing = await db.Set<ReceptionHandover>().SingleOrDefaultAsync(h => h.Id == c.HandoverId && h.DoctorPracticeId == c.PracticeId, ct);
            if (existing is null) return Result<HandoverResponse>.Fail(ReportingErrors.NotFound);
            row = existing;
            byte[] token;
            try { token = Convert.FromBase64String(c.Action?.RowVersion ?? string.Empty); } catch (FormatException) { return Result<HandoverResponse>.Fail(ReportingErrors.Conflict); }
            if (token.Length == 0 || !token.AsSpan().SequenceEqual(row.RowVersion)) return Result<HandoverResponse>.Fail(ReportingErrors.Conflict);
            db.Entry(row).Property(h => h.RowVersion).OriginalValue = token;
            var changed = c.Operation switch
            {
                HandoverOperation.Acknowledge => row.Acknowledge(scope.ActorId, clock.UtcNow),
                HandoverOperation.Close => row.Close(c.Actor == ReportActor.Doctor, c.Action?.Outcome ?? string.Empty, scope.ActorId, clock.UtcNow),
                HandoverOperation.Annotate => row.Annotate(c.Action?.Annotation ?? string.Empty, scope.ActorId, clock.UtcNow),
                _ => Result.Fail(OperatingFinanceErrors.Invalid())
            };
            if (changed.IsFailure) return Result<HandoverResponse>.Fail(changed.Errors);
            db.Set<ReceptionHandoverHistory>().Add(row.History.Last());
        }
        idem = new(Guid.NewGuid(), scope.ActorId, operation, key, fingerprint, clock.UtcNow); idem.Complete(row.Id, clock.UtcNow);
        db.FinancialIdempotencyRecords.Add(idem); await db.SaveChangesAsync(ct);
        return Result<HandoverResponse>.Ok(Response(row, scope, await DelegatedPermissionsAsync(scope, ct)));
    }
    private static string Permission(ReportActor actor, HandoverOperation operation, bool view = false)
        => actor == ReportActor.Doctor ? view ? Phase18PermissionNames.DoctorHandoversView : Phase18PermissionNames.DoctorHandoversManage
            : view ? Phase18PermissionNames.HandoverView : operation switch
            { HandoverOperation.Create => Phase18PermissionNames.HandoverCreate, HandoverOperation.Acknowledge => Phase18PermissionNames.HandoverAcknowledge,
                HandoverOperation.Close => Phase18PermissionNames.HandoverClose, _ => Phase18PermissionNames.HandoverAnnotate };
    private async Task<HashSet<(Guid Practice, string Permission)>> DelegatedPermissionsAsync(ReportScope scope, CancellationToken ct)
    {
        if (scope.Actor == ReportActor.Doctor) return [];
        var rows = await (from a in db.ReceptionPracticeAssignments.AsNoTracking()
            join r in db.Receptions.AsNoTracking() on a.ReceptionId equals r.Id
            join d in db.ReceptionPracticeAssignmentPermissions.AsNoTracking() on a.Id equals d.AssignmentId
            join p in db.Permissions.AsNoTracking() on d.PermissionId equals p.Id
            where r.ApplicationUserId == scope.ActorId && a.IsActive && scope.PracticeIds.Contains(a.DoctorPracticeId)
            select new { a.DoctorPracticeId, p.Name }).ToArrayAsync(ct);
        return rows.Select(r => (r.DoctorPracticeId, r.Name)).ToHashSet();
    }
    private static HandoverResponse Response(ReceptionHandover h, ReportScope scope, HashSet<(Guid Practice, string Permission)> delegated)
    {
        bool Can(HandoverOperation op) => scope.Permissions.Contains(Permission(scope.Actor, op)) &&
            (scope.Actor == ReportActor.Doctor || delegated.Contains((h.DoctorPracticeId, Permission(scope.Actor, op))));
        return new(h.Id, h.DoctorPracticeId, h.Summary, h.ReservationId, h.TicketId, h.PreviousHandoverId, h.Status,
            h.CreatedByApplicationUserId, DateTime.SpecifyKind(h.CreatedOnUtc, DateTimeKind.Utc), h.AcknowledgedByApplicationUserId,
            h.AcknowledgedOnUtc, h.ClosedByApplicationUserId, h.ClosedOnUtc, h.ClosureOutcome, Convert.ToBase64String(h.RowVersion),
            [new("Acknowledge", Can(HandoverOperation.Acknowledge) && h.Status == HandoverStatus.Open && h.CreatedByApplicationUserId != scope.ActorId),
             new("Close", Can(HandoverOperation.Close) && (h.Status == HandoverStatus.Acknowledged || h.Status == HandoverStatus.Open && scope.Actor == ReportActor.Doctor)),
             new("Annotate", Can(HandoverOperation.Annotate) && h.Status != HandoverStatus.Closed),
             new("CreateFollowUp", Can(HandoverOperation.Create), UnavailableReasonCode: "Handover.FollowUpRequiresNewRecord")]);
    }
}

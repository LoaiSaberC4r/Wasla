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

internal sealed class InvestigationService(WaslaDbContext db, IReportingAccessGuard guard, IReportingTimeScope time,
    ITicketQueueLock coordination, IDateTimeProvider clock) : IInvestigationService
{
    public async Task<Result<ReportPage<InvestigationResponse>>> ListAsync(InvestigationFilter f, CancellationToken ct)
    {
        var access = await guard.AuthorizeAsync(ReportActor.SuperAdmin, Phase18PermissionNames.InvestigationsView, null, null, ct);
        if (access.IsFailure) return Result<ReportPage<InvestigationResponse>>.Fail(access.Errors);
        var window = time.Resolve(new(f.FromDate, f.ToDate, PageNumber: f.PageNumber, PageSize: f.PageSize, SortDirection: f.SortDirection));
        if (window.IsFailure || f.Status is { } status && !Enum.IsDefined(status) || f.IssueType is { } issue && !Enum.IsDefined(issue) ||
            f.Priority is { } priority && !Enum.IsDefined(priority)) return Result<ReportPage<InvestigationResponse>>.Fail(ReportingErrors.InvalidFilter);
        var bounds = ReportingTimeScope.Bounds(window.Value.FromDate, window.Value.ToDate, window.Value.ReportTimeZoneId);
        var query = db.Set<AdministrativeInvestigation>().AsNoTracking().Where(c => c.CreatedOnUtc >= bounds.FromUtc && c.CreatedOnUtc < bounds.BeforeUtc &&
            (f.Status == null || c.Status == f.Status) && (f.IssueType == null || c.IssueType == f.IssueType) &&
            (f.Priority == null || c.Priority == f.Priority) && (f.AssignedSuperAdminId == null || c.AssignedSuperAdminId == f.AssignedSuperAdminId));
        var count = await query.LongCountAsync(ct);
        var rows = await (f.SortDirection == "asc" ? query.OrderBy(c => c.CreatedOnUtc) : query.OrderByDescending(c => c.CreatedOnUtc))
            .ThenBy(c => c.Id).Skip((f.PageNumber - 1) * f.PageSize).Take(f.PageSize).ToArrayAsync(ct);
        var active = await ActiveAdminsAsync(ct);
        return Result<ReportPage<InvestigationResponse>>.Ok(new(rows.Select(c => Response(c, access.Value, active)).ToArray(), count, f.PageNumber, f.PageSize));
    }
    public async Task<Result<InvestigationResponse>> DetailAsync(Guid id, CancellationToken ct)
    {
        var access = await guard.AuthorizeAsync(ReportActor.SuperAdmin, Phase18PermissionNames.InvestigationsView, null, null, ct);
        if (access.IsFailure) return Result<InvestigationResponse>.Fail(access.Errors);
        var row = await db.Set<AdministrativeInvestigation>().AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);
        return row == null ? Result<InvestigationResponse>.Fail(ReportingErrors.NotFound)
            : Result<InvestigationResponse>.Ok(Response(row, access.Value, await ActiveAdminsAsync(ct)));
    }
    public async Task<Result<ReportPage<InvestigationHistoryResponse>>> HistoryAsync(Guid id, int pageNumber, int pageSize, CancellationToken ct)
    {
        var detail = await DetailAsync(id, ct); if (detail.IsFailure) return Result<ReportPage<InvestigationHistoryResponse>>.Fail(detail.Errors);
        if (pageNumber is < 1 or > 1_000_000 || pageSize is < 1 or > 100) return Result<ReportPage<InvestigationHistoryResponse>>.Fail(ReportingErrors.InvalidFilter);
        var query = db.Set<InvestigationHistory>().AsNoTracking().Where(h => h.CaseId == id);
        var count = await query.LongCountAsync(ct);
        var rows = await query.OrderBy(h => h.SequenceNumber).Skip((pageNumber - 1) * pageSize).Take(pageSize).Select(h =>
            new InvestigationHistoryResponse(h.Id, h.Operation, h.Status, h.ActorApplicationUserId, h.AssignedSuperAdminId,
                h.ResolutionType, h.Note, h.RecordedAtUtc, h.SequenceNumber)).ToArrayAsync(ct);
        return Result<ReportPage<InvestigationHistoryResponse>>.Ok(new(rows, count, pageNumber, pageSize));
    }
    public async Task<Result<InvestigationFilterOptions>> OptionsAsync(CancellationToken ct, ReportOptionsFilter? filter = null)
    {
        var access = await guard.AuthorizeAsync(ReportActor.SuperAdmin, Phase18PermissionNames.InvestigationsView, null, null, ct);
        if (access.IsFailure) return Result<InvestigationFilterOptions>.Fail(access.Errors);
        var f = filter ?? new();
        if (f.PracticeId != null || f.PageNumber is < 1 or > 1_000_000 || f.PageSize is < 1 or > 100 || f.Search?.Length > 100)
            return Result<InvestigationFilterOptions>.Fail(ReportingErrors.InvalidFilter);
        var query = from a in db.SuperAdmins.AsNoTracking() join u in db.ApplicationUsers.AsNoTracking() on a.ApplicationUserId equals u.Id
            where !a.IsDeleted && u.IsActive && !u.IsFirstLogin && db.UserRoles.Any(r => r.ApplicationUserId == u.Id && r.RoleId == SystemRoleIds.SuperAdmin) &&
                (f.Search == null || a.NameAr.Contains(f.Search) || a.NameEn != null && a.NameEn.Contains(f.Search))
            select a;
        var total = await query.LongCountAsync(ct);
        var admins = await query.OrderBy(a => a.NameAr).ThenBy(a => a.Id).Skip((f.PageNumber - 1) * f.PageSize).Take(f.PageSize)
            .Select(a => new ReportOption(a.Id.ToString(), a.NameAr, a.NameEn, a.Id)).ToArrayAsync(ct);
        return Result<InvestigationFilterOptions>.Ok(new(
            [new("Open", "مفتوح", "Open"), new("InReview", "قيد المراجعة", "In review"), new("Resolved", "تم الحل", "Resolved"), new("Closed", "مغلق", "Closed")],
            [new("DoctorVerification", "التحقق من الطبيب", "Doctor verification"), new("ApprovalIncident", "إجراء اعتماد", "Approval incident"),
                new("BackgroundJobFailure", "فشل مهمة", "Background job failure"), new("OutboxProblem", "مشكلة صندوق الإرسال", "Outbox problem"),
                new("DataQuality", "جودة البيانات", "Data quality"), new("AdministrativeSupport", "دعم إداري", "Administrative support"), new("SecurityMetadata", "بيانات أمنية وصفية", "Security metadata")],
            [new("Normal", "عادي", "Normal"), new("Urgent", "عاجل", "Urgent")],
            [new("Corrected", "تم التصحيح", "Corrected"), new("Verified", "تم التحقق", "Verified"), new("NoActionRequired", "لا إجراء مطلوب", "No action required"), new("InvalidCase", "حالة غير صحيحة", "Invalid case")],
            [new("None", "بدون رابط", "No link"), new("Doctor", "طبيب", "Doctor"), new("SpecializationRequest", "طلب تخصص", "Specialization request"),
                new("BackgroundJobRun", "تشغيل مهمة", "Background job run"), new("OutboxMessage", "رسالة صندوق الإرسال", "Outbox message")], admins, total, f.PageNumber, f.PageSize));
    }
    public async Task<Result<InvestigationResponse>> WriteAsync(WriteInvestigationCommand c, CancellationToken ct)
    {
        if (!Enum.IsDefined(c.Operation) || string.IsNullOrWhiteSpace(c.IdempotencyKey) || c.IdempotencyKey.Length > 200)
            return Result<InvestigationResponse>.Fail(OperatingFinanceErrors.Invalid());
        var access = await guard.AuthorizeAsync(ReportActor.SuperAdmin, Permission(c.Operation), null, null, ct);
        if (access.IsFailure) return Result<InvestigationResponse>.Fail(access.Errors);
        var view = await guard.AuthorizeAsync(ReportActor.SuperAdmin, Phase18PermissionNames.InvestigationsView, null, null, ct);
        if (view.IsFailure) return Result<InvestigationResponse>.Fail(view.Errors);
        var scope = access.Value; var operation = "P18.Investigation." + c.Operation; var key = c.IdempotencyKey.Trim();
        if (c.CaseId is { } target) await coordination.AcquireIdempotencyAsync(Guid.Empty, "Investigation", target.ToString("N"), ct);
        await coordination.AcquireIdempotencyAsync(scope.ActorId, operation, key, ct);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(c with { IdempotencyKey = string.Empty }))));
        var idem = await db.FinancialIdempotencyRecords.SingleOrDefaultAsync(r => r.ActorApplicationUserId == scope.ActorId && r.Operation == operation && r.IdempotencyKey == key, ct);
        if (idem != null) return idem.RequestFingerprint == fingerprint && idem.ResultId is { } existing ? await DetailAsync(existing, ct)
            : Result<InvestigationResponse>.Fail(OperatingFinanceErrors.State("Investigation.IdempotencyKeyReused"));
        var active = await ActiveAdminsAsync(ct); AdministrativeInvestigation row;
        if (c.Operation == InvestigationOperation.Create && c.Create is { } create)
        {
            var linkValid = create.LinkedResourceType switch
            {
                InvestigationResourceType.None => create.LinkedResourceId == null,
                InvestigationResourceType.Doctor => scope.Permissions.Contains(PermissionNames.DoctorsViewDetails) &&
                    await db.Doctors.AnyAsync(d => d.Id == create.LinkedResourceId, ct),
                InvestigationResourceType.SpecializationRequest => scope.Permissions.Contains(PermissionNames.DoctorSpecializationRequestsViewDetails) &&
                    await db.DoctorSpecializationRequests.AnyAsync(r => r.Id == create.LinkedResourceId, ct),
                InvestigationResourceType.BackgroundJobRun => scope.Permissions.Contains(Phase18PermissionNames.JobsView) &&
                    await db.Set<OperatingGenerationRun>().AnyAsync(r => r.Id == create.LinkedResourceId, ct),
                InvestigationResourceType.OutboxMessage => scope.Permissions.Contains(Phase18PermissionNames.JobsView) &&
                    await db.EmailOutboxMessages.AnyAsync(r => r.Id == create.LinkedResourceId, ct),
                _ => false
            };
            if (!linkValid) return Result<InvestigationResponse>.Fail(ReportingErrors.NotFound);
            var result = AdministrativeInvestigation.Create(Guid.NewGuid(), create.TitleAr, create.TitleEn, create.Description,
                create.IssueType, create.Priority, create.LinkedResourceType, create.LinkedResourceId, scope.ActorId, clock.UtcNow);
            if (result.IsFailure) return Result<InvestigationResponse>.Fail(result.Errors);
            row = result.Value; db.Set<AdministrativeInvestigation>().Add(row);
        }
        else
        {
            var existing = await db.Set<AdministrativeInvestigation>().SingleOrDefaultAsync(r => r.Id == c.CaseId, ct);
            if (existing == null) return Result<InvestigationResponse>.Fail(ReportingErrors.NotFound);
            row = existing; byte[] token;
            try { token = Convert.FromBase64String(c.Action?.RowVersion ?? string.Empty); } catch (FormatException) { return Result<InvestigationResponse>.Fail(ReportingErrors.Conflict); }
            if (token.Length == 0 || !token.AsSpan().SequenceEqual(row.RowVersion)) return Result<InvestigationResponse>.Fail(ReportingErrors.Conflict);
            db.Entry(row).Property(r => r.RowVersion).OriginalValue = token;
            var assignedActive = row.AssignedSuperAdminId is { } admin && active.Contains(admin);
            var reopenAssignee = c.Action?.AssignedSuperAdminId ?? row.AssignedSuperAdminId;
            if (c.Operation == InvestigationOperation.Reopen && reopenAssignee != row.AssignedSuperAdminId && !scope.Permissions.Contains(Phase18PermissionNames.InvestigationsAssign))
                return Result<InvestigationResponse>.Fail(ReportingErrors.Denied);
            var changed = c.Operation switch
            {
                InvestigationOperation.Assign => row.Assign(c.Action?.AssignedSuperAdminId ?? Guid.Empty, c.Action?.AssignedSuperAdminId is { } a && active.Contains(a), scope.ActorId, clock.UtcNow),
                InvestigationOperation.StartReview => row.StartReview(assignedActive, scope.ActorId, clock.UtcNow),
                InvestigationOperation.Resolve => row.Resolve(c.Action?.ResolutionType, c.Action?.Notes ?? string.Empty, assignedActive, scope.ActorId, clock.UtcNow),
                InvestigationOperation.Close => row.Close(c.Action?.Notes ?? string.Empty, scope.ActorId, clock.UtcNow),
                InvestigationOperation.Reopen => row.Reopen(c.Action?.Notes ?? string.Empty, reopenAssignee is { } selected && active.Contains(selected), scope.ActorId, clock.UtcNow, reopenAssignee),
                _ => Result.Fail(OperatingFinanceErrors.Invalid())
            };
            if (changed.IsFailure) return Result<InvestigationResponse>.Fail(changed.Errors);
            db.Set<InvestigationHistory>().Add(row.History.Last());
        }
        idem = new(Guid.NewGuid(), scope.ActorId, operation, key, fingerprint, clock.UtcNow); idem.Complete(row.Id, clock.UtcNow);
        db.FinancialIdempotencyRecords.Add(idem); await db.SaveChangesAsync(ct);
        return Result<InvestigationResponse>.Ok(Response(row, scope, active));
    }
    private async Task<HashSet<Guid>> ActiveAdminsAsync(CancellationToken ct)
        => (await (from a in db.SuperAdmins.AsNoTracking() join u in db.ApplicationUsers.AsNoTracking() on a.ApplicationUserId equals u.Id
            join role in db.UserRoles.AsNoTracking() on u.Id equals role.ApplicationUserId
            where !a.IsDeleted && u.IsActive && !u.IsFirstLogin && role.RoleId == SystemRoleIds.SuperAdmin
            select a.Id).ToArrayAsync(ct)).ToHashSet();
    private static string Permission(InvestigationOperation op) => op switch
    {
        InvestigationOperation.Create => Phase18PermissionNames.InvestigationsCreate, InvestigationOperation.Assign => Phase18PermissionNames.InvestigationsAssign,
        InvestigationOperation.StartReview => Phase18PermissionNames.InvestigationsStartReview, InvestigationOperation.Resolve => Phase18PermissionNames.InvestigationsResolve,
        InvestigationOperation.Close => Phase18PermissionNames.InvestigationsClose, _ => Phase18PermissionNames.InvestigationsReopen
    };
    private static InvestigationResponse Response(AdministrativeInvestigation r, ReportScope scope, HashSet<Guid> active)
    {
        bool Can(InvestigationOperation op) => scope.Permissions.Contains(Permission(op));
        var assigned = r.AssignedSuperAdminId is { } id && active.Contains(id);
        return new(r.Id, r.TitleAr, r.TitleEn, r.Description, r.IssueType, r.Priority, r.LinkedResourceType, r.LinkedResourceId,
            r.CreatedByApplicationUserId, r.AssignedSuperAdminId, r.Status, r.ResolutionType, r.ResolutionNotes, r.ClosureNotes,
            DateTime.SpecifyKind(r.CreatedOnUtc, DateTimeKind.Utc), DateTime.SpecifyKind(r.ModifiedOnUtc, DateTimeKind.Utc),
            r.ResolvedOnUtc is { } resolved ? DateTime.SpecifyKind(resolved, DateTimeKind.Utc) : null,
            r.ClosedOnUtc is { } closed ? DateTime.SpecifyKind(closed, DateTimeKind.Utc) : null, Convert.ToBase64String(r.RowVersion),
            [new("Assign", Can(InvestigationOperation.Assign) && r.Status is InvestigationStatus.Open or InvestigationStatus.InReview),
                new("StartReview", Can(InvestigationOperation.StartReview) && assigned && r.Status == InvestigationStatus.Open),
                new("Resolve", Can(InvestigationOperation.Resolve) && assigned && r.Status == InvestigationStatus.InReview),
                new("Close", Can(InvestigationOperation.Close) && r.Status is InvestigationStatus.Open or InvestigationStatus.Resolved),
                new("Reopen", Can(InvestigationOperation.Reopen) && (assigned || Can(InvestigationOperation.Assign) && active.Count > 0) && r.Status is InvestigationStatus.Resolved or InvestigationStatus.Closed)]);
    }
}

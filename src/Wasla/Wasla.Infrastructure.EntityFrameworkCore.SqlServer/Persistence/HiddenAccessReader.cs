using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Clinical;
using Wasla.Domain.Clinical;
using Wasla.Domain.Common;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class CurrentTreatmentReader(WaslaDbContext db) : ICurrentTreatmentReader
{
    public Task<TreatmentContext?> CurrentAsync(Guid doctorId, Guid practiceId, Guid ticketId, CancellationToken ct)
        => (from t in db.Tickets.AsNoTracking() join e in db.MedicalEncounters.AsNoTracking() on t.Id equals e.TicketId
            join p in db.DoctorPractices.AsNoTracking() on t.DoctorPracticeId equals p.Id
            where t.Id == ticketId && t.DoctorId == doctorId && e.DoctorId == doctorId && t.DoctorPracticeId == practiceId &&
                e.DoctorPracticeId == practiceId && e.PatientId == t.PatientId && t.Status == TicketStatus.InProgress &&
                e.Status == EncounterStatus.InProgress && p.IsActive && p.DoctorId == doctorId
            select new TreatmentContext(t.PatientId, e.Id)).SingleOrDefaultAsync(ct);
}
internal sealed record RequestCursor(string Scope, DateTime Time, Guid Id);
internal sealed class HiddenAccessReader(WaslaDbContext db, IMedicalRecordTokens tokens, MedicalRecordAuditWriter audit) : IHiddenAccessReader
{
    private IQueryable<HiddenEncounterAccessRequest> Requests => db.Set<HiddenEncounterAccessRequest>().AsNoTracking();
    private IQueryable<Guid> LiveIds => from r in Requests
        join s in db.MedicalEncounters on r.HiddenEncounterId equals s.Id
        join original in db.Tickets on s.TicketId equals original.Id
        join t in db.Tickets on r.RequestingTicketId equals t.Id
        join e in db.MedicalEncounters on r.RequestingEncounterId equals e.Id
        join p in db.DoctorPractices on r.RequestingPracticeId equals p.Id
        join d in db.Doctors on r.RequestingDoctorId equals d.Id
        join u in db.ApplicationUsers on d.ApplicationUserId equals u.Id
        where s.Status == EncounterStatus.Completed && s.Visibility == EncounterVisibility.Hidden && s.DoctorId == r.OwningDoctorId &&
            original.Status == TicketStatus.Completed && original.PatientId == s.PatientId && original.DoctorId == s.DoctorId && original.DoctorPracticeId == s.DoctorPracticeId &&
            s.PatientId == r.PatientId && s.VisibilityRevision == (r.VisibilityRevisionAtApproval ?? r.VisibilityRevisionAtRequest) &&
            t.Status == TicketStatus.InProgress && e.Status == EncounterStatus.InProgress && t.Id == e.TicketId &&
            t.PatientId == r.PatientId && e.PatientId == r.PatientId && t.DoctorId == r.RequestingDoctorId && e.DoctorId == r.RequestingDoctorId &&
            t.DoctorPracticeId == r.RequestingPracticeId && e.DoctorPracticeId == r.RequestingPracticeId && p.IsActive &&
            p.DoctorId == d.Id && d.ApprovalStatus == DoctorApprovalStatus.Approved && u.IsActive && !u.IsFirstLogin &&
            db.UserRoles.Any(ur => ur.ApplicationUserId == u.Id && db.RolePermissions.Any(rp => rp.RoleId == ur.RoleId && rp.PermissionId == SystemPermissionIds.For(PermissionNames.MedicalRecordsViewTreatingPatient)))
        select r.Id;
    public Task<RequestRouting?> RoutingAsync(Guid id, CancellationToken ct) => (from r in Requests join e in db.MedicalEncounters on r.HiddenEncounterId equals e.Id
        where r.Id == id select new RequestRouting(r.RequestingTicketId, r.HiddenEncounterId, r.OwningDoctorId, e.DoctorPracticeId)).SingleOrDefaultAsync(ct);
    private static DateTime Utc(DateTime time) => DateTime.SpecifyKind(time, DateTimeKind.Utc);
    private static HiddenAccessResponse Map(HiddenEncounterAccessRequest r, bool live, bool owner, MedicalRecordActor actor, RequestOwnerDetails? details = null)
    {
        var effective = r.EffectiveStatus(live);
        var pending = live && r.Status == HiddenAccessRequestStatus.Pending;
        var granted = live && r.Status == HiddenAccessRequestStatus.Approved;
        bool Has(string p) => actor.Permissions.Contains(p, StringComparer.OrdinalIgnoreCase);
        return new(r.Id, r.Status, effective, r.Reason, Utc(r.RequestedAtUtc), r.DecidedAtUtc.HasValue ? Utc(r.DecidedAtUtc.Value) : null, r.RevokedAtUtc.HasValue ? Utc(r.RevokedAtUtc.Value) : null, true, Convert.ToBase64String(r.RowVersion),
            new(owner && pending && Has(PermissionNames.HiddenEncounterAccessRequestsDecideOwn), owner && pending && Has(PermissionNames.HiddenEncounterAccessRequestsDecideOwn),
                owner && granted && Has(PermissionNames.HiddenEncounterAccessRequestsRevokeOwn), !owner && granted && Has(PermissionNames.MedicalRecordsViewTreatingPatient)),
            !owner && granted && Has(PermissionNames.MedicalRecordsViewTreatingPatient) ? r.HiddenEncounterId : null, details);
    }
    public async Task<HiddenAccessResponse?> ResponseAsync(Guid id, MedicalRecordActor actor, bool owner, CancellationToken ct)
    {
        var r = await Requests.SingleOrDefaultAsync(r => r.Id == id && (owner ? r.OwningDoctorId == actor.DoctorId : r.RequestingDoctorId == actor.DoctorId), ct);
        if (r is null) return null;
        RequestOwnerDetails? details = null;
        if (owner)
            details = await (from s in db.MedicalEncounters join p in db.Patients on s.PatientId equals p.Id
                join d in db.Doctors on r.RequestingDoctorId equals d.Id
                where s.Id == r.HiddenEncounterId && db.DoctorPractices.Any(p => p.Id == s.DoctorPracticeId && p.IsActive && p.DoctorId == actor.DoctorId)
                select new RequestOwnerDetails(new(p.Id, p.NameAr, p.NameEn), s.Id, new(d.Id, d.NameAr, d.NameEn), r.RequestingPracticeId)).SingleOrDefaultAsync(ct);
        if (owner && details is null) return null;
        return Map(r, await LiveIds.AnyAsync(x => x == id, ct), owner, actor, details);
    }
    public async Task<object?> ReadAsync(HiddenAccessQuery q, MedicalRecordActor actor, CancellationToken ct)
    {
        var sent = q.Read is HiddenAccessRead.Sent or HiddenAccessRead.SentDetail;
        var query = Requests.Where(r => sent ? r.RequestingDoctorId == actor.DoctorId && r.RequestingTicketId == q.TicketId && r.RequestingPracticeId == q.PracticeId :
            r.OwningDoctorId == actor.DoctorId && db.MedicalEncounters.Any(e => e.Id == r.HiddenEncounterId && e.DoctorId == actor.DoctorId &&
                db.DoctorPractices.Any(p => p.Id == e.DoctorPracticeId && p.DoctorId == actor.DoctorId && p.IsActive)));
        if (sent && !await db.Tickets.AnyAsync(t => t.Id == q.TicketId && t.DoctorId == actor.DoctorId && t.DoctorPracticeId == q.PracticeId && db.DoctorPractices.Any(p => p.Id == t.DoctorPracticeId && p.IsActive), ct)) return null;
        if (q.Read == HiddenAccessRead.InboxSummary)
            return new AccessInboxSummary(await query.CountAsync(r => r.Status == HiddenAccessRequestStatus.Pending, ct),
                await query.CountAsync(r => r.Status == HiddenAccessRequestStatus.Pending && LiveIds.Contains(r.Id), ct),
                await query.CountAsync(r => r.Status == HiddenAccessRequestStatus.Approved && LiveIds.Contains(r.Id), ct));
        if (q.RequestId.HasValue)
        {
            if (!await query.AnyAsync(r => r.Id == q.RequestId, ct)) return null;
            if (q.Read == HiddenAccessRead.History)
                return await db.Set<HiddenEncounterAccessRequestHistory>().AsNoTracking().Where(h => h.RequestId == q.RequestId).OrderBy(h => h.OccurredAtUtc).ThenBy(h => h.Id)
                    .Select(h => new RequestHistoryResponse(h.Id, h.Action, h.ActorApplicationUserId, DateTime.SpecifyKind(h.OccurredAtUtc, DateTimeKind.Utc))).ToArrayAsync(ct);
            var value = await ResponseAsync(q.RequestId.Value, actor, !sent, ct);
            if (value is not null)
            {
                var patientId = await query.Where(r => r.Id == q.RequestId).Select(r => r.PatientId).SingleAsync(ct);
                await audit.WriteAsync(new(actor.UserId, patientId, MedicalRecordMode.TreatingDoctor, actor.DoctorId), "AccessRequestRead", "access-request", q.RequestId, null, ct);
            }
            return value;
        }
        if (!sent && q.PracticeId.HasValue) query = query.Where(r => db.MedicalEncounters.Any(e => e.Id == r.HiddenEncounterId && e.DoctorPracticeId == q.PracticeId));
        if (q.Status == HiddenAccessRequestStatus.Expired) query = query.Where(r => r.Status == HiddenAccessRequestStatus.Expired || (r.Status == HiddenAccessRequestStatus.Pending || r.Status == HiddenAccessRequestStatus.Approved) && !LiveIds.Contains(r.Id));
        else if (q.Status.HasValue) query = query.Where(r => r.Status == q.Status && (q.Status != HiddenAccessRequestStatus.Pending && q.Status != HiddenAccessRequestStatus.Approved || LiveIds.Contains(r.Id)));
        if (!string.IsNullOrWhiteSpace(q.Search)) query = query.Where(r => db.Patients.Any(p => p.Id == r.PatientId && (p.NameAr.Contains(q.Search) || p.NameEn != null && p.NameEn.Contains(q.Search))));
        var scope = $"{actor.UserId:N}:{q.Read}:{q.TicketId}:{q.PracticeId}:{q.Status}:{q.Search}";
        var key = q.Cursor is null ? null : tokens.Read<RequestCursor>("Requests", q.Cursor);
        if (q.Cursor is not null && (key is null || key.Scope != scope)) throw new ArgumentException("Invalid cursor.", nameof(q));
        if (key is not null) query = query.Where(r => r.RequestedAtUtc < key.Time || r.RequestedAtUtc == key.Time && r.Id.CompareTo(key.Id) > 0);
        var rows = await query.OrderByDescending(r => r.RequestedAtUtc).ThenBy(r => r.Id).Take(q.PageSize + 1).ToArrayAsync(ct);
        var page = rows.Take(q.PageSize).ToArray(); var ids = page.Select(r => r.Id).ToArray();
        var live = await LiveIds.Where(id => ids.Contains(id)).ToArrayAsync(ct);
        var ownerDetails = sent ? new Dictionary<Guid, RequestOwnerDetails>() : await (from r in Requests
            join e in db.MedicalEncounters on r.HiddenEncounterId equals e.Id join p in db.Patients on r.PatientId equals p.Id
            join d in db.Doctors on r.RequestingDoctorId equals d.Id where ids.Contains(r.Id) && r.OwningDoctorId == actor.DoctorId
            select new { r.Id, Details = new RequestOwnerDetails(new(p.Id, p.NameAr, p.NameEn), e.Id, new(d.Id, d.NameAr, d.NameEn), r.RequestingPracticeId) }).ToDictionaryAsync(x => x.Id, x => x.Details, ct);
        foreach (var patientId in page.Select(r => r.PatientId).Distinct())
            await audit.WriteAsync(new(actor.UserId, patientId, MedicalRecordMode.TreatingDoctor, actor.DoctorId, q.PracticeId, q.TicketId), "AccessRequestsRead", "access-request", null, null, ct);
        return new RecordPage<HiddenAccessResponse>(page.Select(r => Map(r, live.Contains(r.Id), !sent, actor, ownerDetails.GetValueOrDefault(r.Id))).ToArray(),
            rows.Length > q.PageSize ? tokens.Protect("Requests", new RequestCursor(scope, page[^1].RequestedAtUtc, page[^1].Id)) : null, rows.Length > q.PageSize);
    }
}

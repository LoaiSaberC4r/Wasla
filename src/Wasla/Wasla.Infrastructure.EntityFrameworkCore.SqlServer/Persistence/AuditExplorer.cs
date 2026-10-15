using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using BuildingBlock.Application.Time;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Archives;
using Wasla.Application.Features.Reporting;
using Wasla.Application.Persistence;
using Wasla.Domain.Archives;
using Wasla.Domain.Clinical;
using Wasla.Domain.Common;
using Wasla.Domain.Operations;
using Wasla.Domain.Payments;
using Wasla.Domain.Reporting;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class AuditExplorer(WaslaDbContext db,IReportingAccessGuard guard,ArchiveTokens tokens,IDateTimeProvider clock,
    ICurrentUser user):IAuditExplorer
{
    private sealed class Row
    {
        public Guid Id { get; init; }
        public DateTime Time { get; init; }
        public Guid? Practice { get; init; }
        public Guid? Actor { get; init; }
        public Guid? Entity { get; init; }
        public string Action { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public string Outcome { get; init; } = "Succeeded";
        public string? FailureCode { get; init; }
        public string? CorrelationId { get; init; }
        public Guid? RunId { get; init; }
    }
    private sealed record Source(string Name,IQueryable<Row> Query);
    private Task<Result<ReportScope>> Access(ReportActor actor,Guid? practice,bool sensitive,CancellationToken ct)=>guard.AuthorizeAsync(actor,
        actor==ReportActor.Doctor?(sensitive?Phase18PartTwoPermissionNames.SensitivePractice:Phase18PartTwoPermissionNames.AuditPractice):
        actor==ReportActor.Reception?Phase18PartTwoPermissionNames.AuditOwnActivity:sensitive?Phase18PartTwoPermissionNames.SensitiveRestricted:Phase18PartTwoPermissionNames.AuditPlatform,practice,null,ct);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1305", Justification = "EF translates integer event codes to invariant SQL casts; the provider overload is not SQL-translatable.")]
    private List<Source> Sources(ReportScope scope,bool sensitive)
    {
        var actor = scope.Actor;
        var list=new List<Source>();
        list.Add(new("Archive",db.ArchiveAuditEvents.AsNoTracking().Where(x=>!sensitive||x.Action=="ArchivedTicketOpened"||x.Action=="RestrictedAuditSearched")
            .Select(x=>new Row { Id = x.Id, Time = x.OccurredAtUtc, Practice = x.DoctorPracticeId, Actor = x.ActorId, Entity = x.EntityId, Action = x.Action, Type = x.EntityType, Outcome = x.Outcome, FailureCode = x.FailureCode, CorrelationId = x.CorrelationId, RunId = x.RunId })));
        if(!sensitive)
        {
            var selectedPractice = scope.PracticeIds.Count == 1 ? (Guid?)scope.PracticeIds[0] : null;
            var scopeToken = selectedPractice == null ? null : "\"" + selectedPractice.Value.ToString() + "\"";
            var exports = db.Set<ReportExportAudit>().AsNoTracking().Where(x => actor == ReportActor.SuperAdmin ? x.ActorRole == "SuperAdmin" : x.ActorApplicationUserId == scope.ActorId && x.ActorRole == (actor == ReportActor.Doctor ? "Doctor" : "Reception"));
            if (scopeToken != null) exports = exports.Where(x => x.ScopeSnapshotJson.Contains(scopeToken));
            list.Add(new("Export", exports.Select(x => new Row { Id=x.Id, Time=x.RecordedAtUtc, Practice=selectedPractice, Actor=x.ActorApplicationUserId, Entity=null, Action="ReportExported", Type="Export", Outcome=x.Outcome == "Completed" ? "Succeeded" : x.Outcome == "DeniedBeforeRelease" ? "Denied" : "Failed", FailureCode=x.FailureCode })));

            list.Add(new("TicketHistory",from h in db.AllTicketHistories join r in db.TicketRegistry on h.TicketId equals r.Id select new Row { Id = h.Id, Time = h.OccurredOnUtc, Practice = r.DoctorPracticeId, Actor = h.ActorApplicationUserId, Entity = h.TicketId, Action = "TicketHistory."+((int)h.EventType).ToString(), Type = "Ticket" }));
            list.Add(new("ReservationHistory",from h in db.AllReservationHistories join r in db.ReservationRegistry on h.ReservationId equals r.Id select new Row { Id = h.Id, Time = h.OccurredOnUtc, Practice = r.DoctorPracticeId, Actor = h.PerformedByApplicationUserId, Entity = h.ReservationId, Action = "ReservationHistory."+((int)h.EventType).ToString(), Type = "Reservation" }));
            list.Add(new("Handover",from h in db.Set<ReceptionHandoverHistory>() join r in db.Set<ReceptionHandover>() on h.HandoverId equals r.Id select new Row { Id = h.Id, Time = h.RecordedAtUtc, Practice = r.DoctorPracticeId, Actor = h.ActorApplicationUserId, Entity = h.HandoverId, Action = "Handover."+h.Operation, Type = "Handover" }));
            if(actor==ReportActor.SuperAdmin)list.Add(new("Investigation",db.Set<InvestigationHistory>().Select(h=>new Row { Id = h.Id, Time = h.RecordedAtUtc, Practice = null, Actor = h.ActorApplicationUserId, Entity = h.CaseId, Action = "Investigation."+h.Operation, Type = "Investigation" })));
            if(actor==ReportActor.SuperAdmin)list.Add(new("DoctorGovernance",db.DoctorStatusHistories.Select(h=>new Row { Id=h.Id, Time=h.OccurredOnUtc, Practice=null, Actor=h.PerformedByApplicationUserId, Entity=h.DoctorId, Action="DoctorStatus."+((int)h.ToStatus).ToString(), Type="Governance" })));
            if(actor!=ReportActor.SuperAdmin)
            {
                list.Add(new("PaymentCorrection",from h in db.PaymentCorrectionHistories join p in db.Payments on h.PaymentId equals p.Id select new Row { Id = h.Id, Time = h.CorrectedOnUtc, Practice = p.DoctorPracticeId, Actor = h.CorrectedByApplicationUserId, Entity = h.PaymentId, Action = "PaymentCorrected", Type = "Financial" }));
                list.Add(new("Refund",db.Refunds.Select(h=>new Row { Id = h.Id, Time = h.RefundedOnUtc, Practice = h.DoctorPracticeId, Actor = h.RefundedByApplicationUserId, Entity = h.PaymentId, Action = "RefundRecorded", Type = "Financial" })));
                list.Add(new("RefundCorrection",from h in db.RefundCorrectionHistories join p in db.Refunds on h.RefundId equals p.Id select new Row { Id = h.Id, Time = h.CorrectedOnUtc, Practice = p.DoctorPracticeId, Actor = h.CorrectedByApplicationUserId, Entity = h.RefundId, Action = "RefundCorrected", Type = "Financial" }));
            }
        }
        if(sensitive)
        {
            list.Add(new("EncounterAccess",from h in db.EncounterAuditEvents join e in db.MedicalEncounters on h.MedicalEncounterId equals e.Id select new Row { Id = h.Id, Time = h.OccurredAtUtc, Practice = e.DoctorPracticeId, Actor = h.ActorApplicationUserId, Entity = h.MedicalEncounterId, Action = "Encounter."+h.Action, Type = "Encounter" }));
            // Only access events whose encounter provides a verified Practice scope are included.
            list.Add(new("MedicalAccess",from h in db.Set<MedicalRecordAccessAudit>() join e in db.MedicalEncounters on h.TargetEncounterId equals e.Id select new Row { Id = h.Id, Time = h.OccurredAtUtc, Practice = e.DoctorPracticeId, Actor = h.ActorApplicationUserId, Entity = h.TargetEncounterId, Action = "MedicalAccess."+h.Action, Type = "MedicalAccess" }));
            list.Add(new("TreatmentAccess",from h in db.Set<MedicalRecordAccessAudit>() join r in db.TicketRegistry on h.RequestingTicketId equals r.Id where h.TargetEncounterId==null select new Row { Id = h.Id, Time = h.OccurredAtUtc, Practice = r.DoctorPracticeId, Actor = h.ActorApplicationUserId, Entity = h.RequestingTicketId, Action = "MedicalAccess."+h.Action, Type = "MedicalAccess" }));
        }
        return list;
    }
    private static IQueryable<Row> Scoped(IQueryable<Row> query,ReportScope scope)
    {
        if(scope.Actor!=ReportActor.SuperAdmin){var ids=scope.PracticeIds.ToArray();query=query.Where(x=>x.Practice!=null&&ids.Contains(x.Practice.Value));}
        if(scope.Actor==ReportActor.Reception)query=query.Where(x=>x.Actor==scope.ActorId);
        return query;
    }
    public async Task<Result<ArchivePage<SafeAuditEvent>>> SearchAsync(ReportActor actor,Guid? practice,AuditFilter filter,bool sensitive,string? reason,CancellationToken ct)
    {
        if(filter.PageSize is <1 or >100||filter.FromUtc>filter.ToUtc||(filter.Action?.Length??0)>100||(filter.EntityType?.Length??0)>40||(filter.Outcome!=null&&filter.Outcome is not ("Succeeded" or "Failed" or "Denied"))||
            actor==ReportActor.Reception&&filter.ActorId!=null&&filter.ActorId!=user.UserId||sensitive&&actor==ReportActor.SuperAdmin&&(string.IsNullOrWhiteSpace(reason)||reason.Length>1000))return Result<ArchivePage<SafeAuditEvent>>.Fail(ArchiveErrors.InvalidFilter);
        var scope=await Access(actor,practice??filter.PracticeId,sensitive,ct);if(scope.IsFailure)return Result<ArchivePage<SafeAuditEvent>>.Fail(scope.Errors);
        if(filter.ActorId is {} selected&&!await db.ApplicationUsers.AnyAsync(x=>x.Id==selected&&x.UserType!=UserType.Patient,ct))return Result<ArchivePage<SafeAuditEvent>>.Fail(ArchiveErrors.InvalidFilter);
        var binding=ArchiveTokens.Binding(new{actor,practice,sensitive,filter=filter with{Cursor=null}},scope.Value.ActorId,scope.Value.PracticeIds,scope.Value.Permissions);
        ArchiveCursor? cursor;try{cursor=tokens.Decode(filter.Cursor,binding);}catch(FormatException){return Result<ArchivePage<SafeAuditEvent>>.Fail(ArchiveErrors.InvalidCursor);}
        var asOf=cursor?.AsOfUtc??clock.UtcNow;await tokens.InitialiseEventsAsync(ct);
        if(sensitive&&actor==ReportActor.SuperAdmin){db.ArchiveAuditEvents.Add(new("RestrictedAuditSearched","ArchiveControl",null,null,scope.Value.ActorId,clock.UtcNow,reasonHash:Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(reason!.Trim())))));await db.SaveChangesAsync(ct);}
        var merged=new List<(string Source,Row Row)>();
        foreach(var source in Sources(scope.Value,sensitive))
        {
            var query=Scoped(source.Query,scope.Value).Where(x=>x.Time<=asOf);
            if(filter.PracticeId != null) query=query.Where(x=>x.Practice==filter.PracticeId);
            if(filter.FromUtc!=null)query=query.Where(x=>x.Time>=filter.FromUtc);if(filter.ToUtc!=null)query=query.Where(x=>x.Time<=filter.ToUtc);
            if(filter.Action!=null)query=query.Where(x=>x.Action==filter.Action);if(filter.EntityType!=null)query=query.Where(x=>x.Type==filter.EntityType);
            if(filter.Outcome!=null)query=query.Where(x=>x.Outcome==filter.Outcome);
            if(filter.EntityId!=null)query=query.Where(x=>x.Entity==filter.EntityId);if(filter.ActorId!=null)query=query.Where(x=>x.Actor==filter.ActorId);
            if(cursor?.LastTime is {} last){var id=cursor.LastId;var afterSource=string.CompareOrdinal(source.Name,cursor.LastSource)>0;query=query.Where(x=>x.Time<last||x.Time==last&&(x.Id.CompareTo(id)<0||x.Id==id&&afterSource));}
            var rows=await query.OrderByDescending(x=>x.Time).ThenByDescending(x=>x.Id).Take(filter.PageSize+1).ToArrayAsync(ct);
            merged.AddRange(rows.Select(x=>(source.Name,x)));
        }
        var comparer=Comparer<Guid>.Create((a,b)=>db.Database.IsSqlServer()?new System.Data.SqlTypes.SqlGuid(a).CompareTo(new System.Data.SqlTypes.SqlGuid(b)):a.CompareTo(b));
        var sorted=merged.OrderByDescending(x=>x.Row.Time).ThenByDescending(x=>x.Row.Id,comparer).ThenBy(x=>x.Source,StringComparer.Ordinal).Take(filter.PageSize+1).ToArray();
        var items=new List<SafeAuditEvent>();foreach(var item in sorted.Take(filter.PageSize))items.Add(await Map(item.Source,item.Row,actor,ct));
        var lastItem=sorted.Take(filter.PageSize).LastOrDefault();
        return Result<ArchivePage<SafeAuditEvent>>.Ok(new(items,sorted.Length>filter.PageSize?tokens.Encode(new(binding,asOf,lastItem.Row.Time,null,lastItem.Row.Id,lastItem.Source)):null,filter.PageSize,asOf));
    }
    private async Task<SafeAuditEvent> Map(string source,Row row,ReportActor actor,CancellationToken ct)
    {
        var staff=row.Actor!=null&&await db.ApplicationUsers.AnyAsync(x=>x.Id==row.Actor&&x.UserType!=UserType.Patient,ct);
        return new(tokens.Event(source,row.Id),row.Time,row.Action,row.Type,row.Entity,row.Practice,staff?row.Actor:null,row.Actor==null?"System":staff?"Staff":"PatientRedacted",row.Outcome,row.FailureCode,row.CorrelationId,row.RunId);
    }
    public async Task<Result<SafeAuditEvent>> DetailAsync(ReportActor actor,Guid? practice,string eventKey,CancellationToken ct)
    {
        var scope=await Access(actor,practice,false,ct);if(scope.IsFailure)return Result<SafeAuditEvent>.Fail(scope.Errors);
        await tokens.InitialiseEventsAsync(ct);var key=tokens.ResolveEvent(eventKey);if(key==null)return Result<SafeAuditEvent>.Fail(ArchiveErrors.NotFound);
        var source=Sources(scope.Value,false).SingleOrDefault(x=>x.Name==key.Value.Source);if(source==null)return Result<SafeAuditEvent>.Fail(ArchiveErrors.NotFound);
        var row=await Scoped(source.Query,scope.Value).SingleOrDefaultAsync(x=>x.Id==key.Value.Id,ct);
        return row==null?Result<SafeAuditEvent>.Fail(ArchiveErrors.NotFound):Result<SafeAuditEvent>.Ok(await Map(source.Name,row,actor,ct));
    }
    public async Task<Result<AuditOptions>> OptionsAsync(ReportActor actor,Guid? practice,string? search,int pageNumber,int pageSize,CancellationToken ct)
    {
        if(pageNumber is <1 or >100000||pageSize is <1 or >100||search?.Length>100)return Result<AuditOptions>.Fail(ArchiveErrors.InvalidFilter);
        var scope=await Access(actor,practice,false,ct);var sensitive=false;
        if(scope.IsFailure&&actor!=ReportActor.Reception){scope=await Access(actor,practice,true,ct);sensitive=true;}
        if(scope.IsFailure)return Result<AuditOptions>.Fail(scope.Errors);
        var actorIds=db.ApplicationUsers.AsNoTracking().Where(x=>x.UserType!=UserType.Patient);
        // Scoped actors must have actual activity in a currently readable Practice.
        if(actor==ReportActor.Reception)actorIds=actorIds.Where(x=>x.Id==scope.Value.ActorId);
        else if(actor==ReportActor.Doctor)
        {
            IQueryable<Guid?>? activityActors=null;
            foreach(var source in Sources(scope.Value,sensitive))
            {
                var ids=Scoped(source.Query,scope.Value).Where(x=>x.Actor!=null).Select(x=>x.Actor);
                activityActors=activityActors==null?ids:activityActors.Concat(ids);
            }
            actorIds=actorIds.Where(x=>activityActors!.Contains(x.Id));
        }
        if(search is{Length:>0})actorIds=actorIds.Where(x=>x.UserName.Contains(search));
        var total=await actorIds.LongCountAsync(ct);var actors=await actorIds.OrderBy(x=>x.UserName).ThenBy(x=>x.Id).Skip((pageNumber-1)*pageSize).Take(pageSize).Select(x=>new ActorOption(x.Id,x.UserName,x.UserName)).ToArrayAsync(ct);
        var practiceQuery=db.DoctorPractices.AsNoTracking();if(actor!=ReportActor.SuperAdmin){var ids=scope.Value.PracticeIds.ToArray();practiceQuery=practiceQuery.Where(x=>ids.Contains(x.Id));}
        if(search is{Length:>0})practiceQuery=practiceQuery.Where(x=>x.NameAr.Contains(search)||x.NameEn!=null&&x.NameEn.Contains(search));
        var practiceTotal=await practiceQuery.LongCountAsync(ct);var practicesList=await practiceQuery.OrderBy(x=>x.NameAr).ThenBy(x=>x.Id).Skip((pageNumber-1)*pageSize).Take(pageSize).Select(x=>new PracticeOption(x.Id,x.NameAr,x.NameEn)).ToArrayAsync(ct);
        var actions=new List<string>();var types=new List<string>();foreach(var source in Sources(scope.Value,sensitive)){actions.AddRange(await Scoped(source.Query,scope.Value).Select(x=>x.Action).Distinct().Take(100).ToArrayAsync(ct));types.AddRange(await Scoped(source.Query,scope.Value).Select(x=>x.Type).Distinct().Take(30).ToArrayAsync(ct));}
        return Result<AuditOptions>.Ok(new(actions.Distinct().Order().Select(x=>new ArchiveOption(x,ArchiveLabels.Arabic(x),ArchiveLabels.English(x))).ToArray(),types.Distinct().Order().Select(x=>new ArchiveOption(x,ArchiveLabels.Arabic(x),ArchiveLabels.English(x))).ToArray(),[new("Succeeded","نجح","Succeeded"),new("Failed","فشل","Failed"),new("Denied","مرفوض","Denied")],new(actors,pageNumber,pageSize,total),new(practicesList,pageNumber,pageSize,practiceTotal)));
    }
}

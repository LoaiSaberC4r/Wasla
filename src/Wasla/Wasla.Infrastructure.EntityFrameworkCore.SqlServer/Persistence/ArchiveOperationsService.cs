using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Archives;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Archives;
using Wasla.Domain.Security;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class ArchiveOperationsService(WaslaDbContext db, IReportingAccessGuard guard, ArchiveEngine engine,
    ArchiveTokens tokens, IDateTimeProvider clock, IOptions<ArchiveOptions> options) : IArchiveOperations
{
    private Task<Result<ReportScope>> Access(string permission, CancellationToken ct) => guard.AuthorizeAsync(ReportActor.SuperAdmin,permission,null,null,ct);
    private static readonly string[] RunOutcomes = ["Running","Succeeded","CompletedWithExceptions","Failed","Paused"];
    private static readonly string[] FailureCodes = ["UnverifiedDayClosure","MissingPayment","PaymentMismatch","EncounterMismatch","HistoryMismatch","ReservationMismatch","IdentityMismatch","DatabaseFailure"];
    private async Task<ArchiveControlState> State(CancellationToken ct)
    {
        var state=await db.ArchiveControlStates.SingleOrDefaultAsync(ct);
        if(state!=null) return state;
        state=new(); db.ArchiveControlStates.Add(state); await db.SaveChangesAsync(ct); return state;
    }
    private static ArchiveControlResponse Response(ArchiveControlState state, ReportScope scope, bool ready) => new(state.Paused?"Paused":"Running",Convert.ToBase64String(state.RowVersion),
        !state.Paused&&scope.Permissions.Contains(Phase18PartTwoPermissionNames.Pause),state.Paused&&ready&&scope.Permissions.Contains(Phase18PartTwoPermissionNames.Resume),state.PausedAtUtc,state.PausedByActorId);
    public async Task<Result<ArchiveHealth>> HealthAsync(CancellationToken ct)
    {
        var access=await Access(Phase18PartTwoPermissionNames.Health,ct); if(access.IsFailure) return Result<ArchiveHealth>.Fail(access.Errors);
        var state=await State(ct); var ready=await engine.ReadyAsync(ct); var last=await db.ArchiveRuns.AsNoTracking().OrderByDescending(x=>x.StartedAtUtc).FirstOrDefaultAsync(ct);
        var success=await db.ArchiveRuns.AsNoTracking().Where(x=>x.Outcome=="Succeeded"||x.Outcome=="CompletedWithExceptions").OrderByDescending(x=>x.EndedAtUtc).Select(x=>x.EndedAtUtc).FirstOrDefaultAsync(ct);
        var runsReadable=access.Value.Permissions.Contains(Phase18PartTwoPermissionNames.Runs);
        return Result<ArchiveHealth>.Ok(new(options.Value.Enabled,ready,state.Paused,last?.Mode??"Idle",success,last?.StartedAtUtc,
            await db.ArchiveRuns.CountAsync(x=>x.EndedAtUtc==null,ct),await db.ArchiveIntegrityIssues.LongCountAsync(ct),Response(state,access.Value,ready && options.Value.Enabled),
            runsReadable&&last?.EndedAtUtc!=null?last.Id:null,runsReadable&&last?.EndedAtUtc==null?last?.Id:null));
    }
    private static ArchiveRunItem Run(ArchiveRun row)=>new(row.Id,row.Mode,row.Outcome,row.StartedAtUtc,row.EndedAtUtc,row.Scanned,row.ArchivedTickets,row.ArchivedReservations,row.Excluded,row.CorrelationId);
    private static ArchiveExceptionItem Exception(ArchiveIntegrityIssue row)=>new(row.Id,row.EntityId,row.DoctorPracticeId,row.RunId,row.EntityType,row.FailureCode,row.OccurredAtUtc);
    private static bool Valid(ArchiveOperationsFilter f)=> f.PageSize is >=1 and <=100 && (f.FromUtc==null||f.ToUtc==null||f.FromUtc<=f.ToUtc) &&
        (f.Mode==null||f.Mode is "NormalEndOfDay" or "HistoricalBackfill") && (f.Outcome==null||f.Outcome is "Running" or "Succeeded" or "CompletedWithExceptions" or "Failed" or "Paused") &&
        (f.FailureCode==null||FailureCodes.Contains(f.FailureCode))&&(f.EntityType==null||f.EntityType is "Ticket" or "Reservation") && (f.Type==null||f.Type=="DataIntegrity");
    public async Task<Result<ArchivePage<ArchiveRunItem>>> RunsAsync(ArchiveOperationsFilter filter, CancellationToken ct)
    {
        var scope=await Access(Phase18PartTwoPermissionNames.Runs,ct);if(scope.IsFailure)return Result<ArchivePage<ArchiveRunItem>>.Fail(scope.Errors);
        if(!Valid(filter))return Result<ArchivePage<ArchiveRunItem>>.Fail(ArchiveErrors.InvalidFilter);
        var binding=ArchiveTokens.Binding(new{type="Run",filter=filter with{Cursor=null}},scope.Value.ActorId,scope.Value.PracticeIds,scope.Value.Permissions);
        ArchiveCursor? cursor;try{cursor=tokens.Decode(filter.Cursor,binding);}catch(FormatException){return Result<ArchivePage<ArchiveRunItem>>.Fail(ArchiveErrors.InvalidCursor);}
        var asOf=cursor?.AsOfUtc??clock.UtcNow;var query=db.ArchiveRuns.AsNoTracking().Where(x=>x.StartedAtUtc<=asOf);
        if(filter.FromUtc!=null)query=query.Where(x=>x.StartedAtUtc>=filter.FromUtc);if(filter.ToUtc!=null)query=query.Where(x=>x.StartedAtUtc<=filter.ToUtc);
        if(filter.Mode!=null)query=query.Where(x=>x.Mode==filter.Mode);if(filter.Outcome!=null)query=query.Where(x=>x.Outcome==filter.Outcome);
        if(cursor?.LastTime is {} last){var id=cursor.LastId;query=query.Where(x=>x.StartedAtUtc<last||x.StartedAtUtc==last&&x.Id.CompareTo(id)<0);}
        var rows=await query.OrderByDescending(x=>x.StartedAtUtc).ThenByDescending(x=>x.Id).Take(filter.PageSize+1).ToArrayAsync(ct);var items=rows.Take(filter.PageSize).Select(Run).ToArray();
        return Result<ArchivePage<ArchiveRunItem>>.Ok(new(items,rows.Length>filter.PageSize?tokens.Encode(new(binding,asOf,items[^1].StartedAtUtc,null,items[^1].RunId)):null,filter.PageSize,asOf));
    }
    public async Task<Result<ArchiveRunItem>> RunAsync(Guid id,CancellationToken ct)
    {var access=await Access(Phase18PartTwoPermissionNames.Runs,ct);if(access.IsFailure)return Result<ArchiveRunItem>.Fail(access.Errors);var row=await db.ArchiveRuns.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);return row==null?Result<ArchiveRunItem>.Fail(ArchiveErrors.NotFound):Result<ArchiveRunItem>.Ok(Run(row));}
    public async Task<Result<ArchivePage<ArchiveExceptionItem>>> ExceptionsAsync(ArchiveOperationsFilter filter,CancellationToken ct)
    {
        var scope=await Access(Phase18PartTwoPermissionNames.Failures,ct);if(scope.IsFailure)return Result<ArchivePage<ArchiveExceptionItem>>.Fail(scope.Errors);
        if(!Valid(filter))return Result<ArchivePage<ArchiveExceptionItem>>.Fail(ArchiveErrors.InvalidFilter);
        if(filter.PracticeId!=null&&!scope.Value.PracticeIds.Contains(filter.PracticeId.Value))return Result<ArchivePage<ArchiveExceptionItem>>.Fail(ArchiveErrors.NotFound);
        var binding=ArchiveTokens.Binding(new{type="Exception",filter=filter with{Cursor=null}},scope.Value.ActorId,scope.Value.PracticeIds,scope.Value.Permissions);
        ArchiveCursor? cursor;try{cursor=tokens.Decode(filter.Cursor,binding);}catch(FormatException){return Result<ArchivePage<ArchiveExceptionItem>>.Fail(ArchiveErrors.InvalidCursor);}
        var asOf=cursor?.AsOfUtc??clock.UtcNow;var query=db.ArchiveIntegrityIssues.AsNoTracking().Where(x=>x.OccurredAtUtc<=asOf);
        if(filter.FromUtc!=null)query=query.Where(x=>x.OccurredAtUtc>=filter.FromUtc);if(filter.ToUtc!=null)query=query.Where(x=>x.OccurredAtUtc<=filter.ToUtc);
        if(filter.FailureCode!=null)query=query.Where(x=>x.FailureCode==filter.FailureCode);if(filter.EntityType!=null)query=query.Where(x=>x.EntityType==filter.EntityType);if(filter.PracticeId!=null)query=query.Where(x=>x.DoctorPracticeId==filter.PracticeId);
        if(cursor?.LastTime is {} last){var id=cursor.LastId;query=query.Where(x=>x.OccurredAtUtc<last||x.OccurredAtUtc==last&&x.Id.CompareTo(id)<0);}
        var rows=await query.OrderByDescending(x=>x.OccurredAtUtc).ThenByDescending(x=>x.Id).Take(filter.PageSize+1).ToArrayAsync(ct);var items=rows.Take(filter.PageSize).Select(Exception).ToArray();
        return Result<ArchivePage<ArchiveExceptionItem>>.Ok(new(items,rows.Length>filter.PageSize?tokens.Encode(new(binding,asOf,items[^1].OccurredAtUtc,null,items[^1].ExceptionId)):null,filter.PageSize,asOf));
    }
    public async Task<Result<ArchiveExceptionItem>> ExceptionAsync(Guid id,CancellationToken ct)
    {var access=await Access(Phase18PartTwoPermissionNames.Failures,ct);if(access.IsFailure)return Result<ArchiveExceptionItem>.Fail(access.Errors);var row=await db.ArchiveIntegrityIssues.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);return row==null?Result<ArchiveExceptionItem>.Fail(ArchiveErrors.NotFound):Result<ArchiveExceptionItem>.Ok(Exception(row));}
    public async Task<Result<ArchiveControlResponse>> ControlAsync(bool pause,ArchiveControlBody body,string key,CancellationToken ct)
    {
        var access=await Access(pause?Phase18PartTwoPermissionNames.Pause:Phase18PartTwoPermissionNames.Resume,ct);if(access.IsFailure)return Result<ArchiveControlResponse>.Fail(access.Errors);
        if(!access.Value.Permissions.Contains(Phase18PartTwoPermissionNames.Health))return Result<ArchiveControlResponse>.Fail(ArchiveErrors.Denied);
        if(string.IsNullOrWhiteSpace(body.Reason)||body.Reason.Length>1000||string.IsNullOrWhiteSpace(key)||key.Length>200||string.IsNullOrWhiteSpace(body.RowVersion))return Result<ArchiveControlResponse>.Fail(ArchiveErrors.InvalidFilter);
        key = key.Trim();
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{pause}|{body.RowVersion}|{body.Reason.Trim()}")));
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        if(db.Database.IsSqlServer())await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sp_getapplock @Resource=N'Wasla:ArchiveControl', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=1000; IF @r<0 THROW 51000, 'Archive.ControlBusy',1;",ct);
        var operation = pause ? "Pause" : "Resume";
        var replay=await db.ArchiveControlRequests.SingleOrDefaultAsync(x=>x.ActorId==access.Value.ActorId&&x.Operation==operation&&x.IdempotencyKey==key,ct);
        if(replay!=null)return replay.Fingerprint!=fingerprint?Result<ArchiveControlResponse>.Fail(ArchiveErrors.IdempotencyConflict):Result<ArchiveControlResponse>.Ok(new(replay.Paused?"Paused":"Running",replay.ResultRowVersion,replay.CanPause,replay.CanResume,replay.PausedAtUtc,replay.PausedByActorId));
        var state=await State(ct);
        if(Convert.ToBase64String(state.RowVersion)!=body.RowVersion)return Result<ArchiveControlResponse>.Fail(ArchiveErrors.Conflict);
        if(state.Paused==pause)return Result<ArchiveControlResponse>.Fail(ArchiveErrors.Conflict);
        var ready=await engine.ReadyAsync(ct);if(!pause&&(!ready||!options.Value.Enabled))return Result<ArchiveControlResponse>.Fail(ArchiveErrors.NotReady);
        state.SetPaused(pause,access.Value.ActorId,clock.UtcNow);
        db.ArchiveAuditEvents.Add(new(pause?"ArchivePaused":"ArchiveResumed","ArchiveControl",state.Id,null,access.Value.ActorId,clock.UtcNow,reasonHash:Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body.Reason.Trim())))));
        await db.SaveChangesAsync(ct);var response = Response(state, access.Value, ready && options.Value.Enabled); db.ArchiveControlRequests.Add(new(access.Value.ActorId,key,fingerprint,state,response.CanPause,response.CanResume));await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);
        return Result<ArchiveControlResponse>.Ok(response);
    }
    public async Task<Result<ArchiveOperationsOptions>> OptionsAsync(CancellationToken ct)
    {
        foreach(var permission in new[]{Phase18PartTwoPermissionNames.Health,Phase18PartTwoPermissionNames.Runs,Phase18PartTwoPermissionNames.Failures})
            if((await Access(permission,ct)).IsSuccess)return Result<ArchiveOperationsOptions>.Ok(new([new("NormalEndOfDay","نهاية يوم العمل","End of day"),new("HistoricalBackfill","أرشفة تاريخية","Historical backfill")],
                RunOutcomes.Select(x=>new ArchiveOption(x,ArchiveLabels.Arabic(x),ArchiveLabels.English(x))).ToArray(),FailureCodes.Select(x=>new ArchiveOption(x,ArchiveLabels.Arabic(x),ArchiveLabels.English(x))).ToArray(),[new("Ticket","تذكرة","Ticket"),new("Reservation","حجز","Reservation")]));
        return Result<ArchiveOperationsOptions>.Fail(ArchiveErrors.Denied);
    }
}

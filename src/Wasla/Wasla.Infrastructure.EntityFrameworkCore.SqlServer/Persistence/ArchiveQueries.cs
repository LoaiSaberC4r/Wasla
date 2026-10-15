using Microsoft.EntityFrameworkCore;
using BuildingBlock.Application.Time;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Archives;
using Wasla.Application.Features.Reporting;
using Wasla.Application.Persistence;
using Wasla.Domain.Archives;
using Wasla.Domain.Security;
using Wasla.Domain.Families;
using Wasla.Domain.Reservations;
using Wasla.Domain.Tickets;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class ArchiveQueries(WaslaDbContext db, IReportingAccessGuard guard, ArchiveTokens tokens,
    IDateTimeProvider clock, ICurrentUser user, IWaslaDataStore store, IPatientAccessPolicy family) : IArchiveQueries
{
    private static string Permission(ReportActor actor, bool ticket) => actor switch
    {
        ReportActor.Doctor => ticket ? Phase18PartTwoPermissionNames.TicketsPractice : Phase18PartTwoPermissionNames.ReservationsPractice,
        ReportActor.Reception => ticket ? Phase18PartTwoPermissionNames.TicketsAssigned : Phase18PartTwoPermissionNames.ReservationsAssigned,
        ReportActor.Patient => ticket ? Phase18PartTwoPermissionNames.TicketsOwn : Phase18PartTwoPermissionNames.ReservationsOwn,
        _ => ticket ? Phase18PartTwoPermissionNames.TicketsPlatform : Phase18PartTwoPermissionNames.ReservationsPlatform
    };
    private static bool Valid(ArchiveFilter f, ReportActor actor, bool ticket)
        => f.PageSize is >= 1 and <= 100 && (f.Search?.Length ?? 0) <= 100 && (f.Reference?.Length ?? 0) <= 32 &&
            (f.FromBusinessDate == null || f.ToBusinessDate == null || f.FromBusinessDate <= f.ToBusinessDate) &&
            (ticket ? f.Reference == null : f.TicketNumber == null) &&
            (f.Status == null || (ticket ? Enum.TryParse<TicketStatus>(f.Status, out var ts) && Enum.IsDefined(ts) : Enum.TryParse<ReservationStatus>(f.Status, out var rs) && Enum.IsDefined(rs))) &&
            (actor != ReportActor.SuperAdmin || f.PatientId == null && f.Search == null) &&
            (actor != ReportActor.Patient || f.Search == null && f.PracticeId == null && (!ticket || f.PatientId == null));

    private async Task<bool> DependentAsync(ReportScope scope, Guid subject, CancellationToken ct)
    {
        if (!scope.Permissions.Contains(PermissionNames.ReservationsViewDependents)) return false;
        var actorMember = await store.FindActiveFamilyMemberByPatientIdAsync(scope.PatientId!.Value, ct);
        var patient = await store.FindPatientByIdAsync(subject, ct);
        if (actorMember == null || patient == null) return false;
        var members = await store.ListActiveFamilyMembersAsync(actorMember.FamilyId, ct);
        var target = members.SingleOrDefault(x => x.Member.PatientId == subject)?.Member;
        return target != null && family.CanBookReservation(actorMember, target, patient, DateOnly.FromDateTime(clock.UtcNow));
    }
    public async Task<Result<OptionsPage<ArchivePatientOption>>> PatientsAsync(int pageNumber, int pageSize, CancellationToken ct)
    {
        if (pageNumber is < 1 or > 100000 || pageSize is < 1 or > 100) return Result<OptionsPage<ArchivePatientOption>>.Fail(ArchiveErrors.InvalidFilter);
        var scope = await guard.AuthorizeAsync(ReportActor.Patient, Phase18PartTwoPermissionNames.ReservationsOwn, null, null, ct);
        if (scope.IsFailure) return Result<OptionsPage<ArchivePatientOption>>.Fail(scope.Errors);
        var own = await store.FindPatientByIdAsync(scope.Value.PatientId!.Value, ct);
        if (own == null) return Result<OptionsPage<ArchivePatientOption>>.Fail(ArchiveErrors.NotFound);
        var options = new List<ArchivePatientOption> { new(own.Id, own.NameAr, own.NameEn, true) };
        if (scope.Value.Permissions.Contains(PermissionNames.ReservationsViewDependents))
        {
            var actorMember = await store.FindActiveFamilyMemberByPatientIdAsync(own.Id, ct);
            if (actorMember != null)
                foreach (var member in await store.ListActiveFamilyMembersAsync(actorMember.FamilyId, ct))
                    if (member.Member.PatientId != own.Id && await DependentAsync(scope.Value, member.Member.PatientId, ct))
                    {
                        var patient = await store.FindPatientByIdAsync(member.Member.PatientId, ct);
                        if (patient != null) options.Add(new(patient.Id, patient.NameAr, patient.NameEn, false));
                    }
        }
        return Result<OptionsPage<ArchivePatientOption>>.Ok(new(options.OrderByDescending(x => x.IsSelf).ThenBy(x => x.NameAr).ThenBy(x => x.PatientId).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray(), pageNumber, pageSize, options.Count));
    }

    public async Task<Result<ArchivePage<ArchivedTicketItem>>> TicketsAsync(ReportActor actor, Guid? practice, ArchiveFilter filter, CancellationToken ct)
    {
        if (!Valid(filter, actor, true)) return Result<ArchivePage<ArchivedTicketItem>>.Fail(ArchiveErrors.InvalidFilter);
        var scope = await guard.AuthorizeAsync(actor, Permission(actor, true), practice ?? filter.PracticeId, null, ct);
        if (scope.IsFailure) return Result<ArchivePage<ArchivedTicketItem>>.Fail(scope.Errors);
        var binding = ArchiveTokens.Binding(new { type = "Ticket", actor, filter = filter with { Cursor = null }, practice }, scope.Value.ActorId, scope.Value.PracticeIds, scope.Value.Permissions);
        ArchiveCursor? cursor; try { cursor = tokens.Decode(filter.Cursor, binding); } catch (FormatException) { return Result<ArchivePage<ArchivedTicketItem>>.Fail(ArchiveErrors.InvalidCursor); }
        var watermark = cursor?.Watermark ?? await db.ArchiveProgress.AsNoTracking().Where(x => x.Id == "CommittedSequence").Select(x => x.Sequence).SingleOrDefaultAsync(ct);
        var asOf = cursor?.AsOfUtc ?? clock.UtcNow; var practices = scope.Value.PracticeIds.ToArray();
        var query = from t in db.Set<ArchivedTicket>().AsNoTracking() join r in db.TicketRegistry.AsNoTracking() on t.Id equals r.Id
            where r.IsArchived && r.ArchiveSequence <= watermark && r.ArchivedAtUtc <= asOf && (actor == ReportActor.Patient ? t.PatientId == scope.Value.PatientId : practices.Contains(t.DoctorPracticeId))
            select new { Ticket = t, Registry = r };
        if (filter.FromBusinessDate != null) query = query.Where(x => x.Ticket.BusinessDate >= filter.FromBusinessDate);
        if (filter.ToBusinessDate != null) query = query.Where(x => x.Ticket.BusinessDate <= filter.ToBusinessDate);
        if (filter.Status != null) { var status = Enum.Parse<TicketStatus>(filter.Status); query = query.Where(x => x.Ticket.Status == status); }
        if (filter.TicketNumber != null) query = query.Where(x => x.Ticket.TicketNumber == filter.TicketNumber);
        if (filter.PatientId != null) query = query.Where(x => x.Ticket.PatientId == filter.PatientId);
        if (filter.Search is { Length: > 0 } text) query = query.Where(x => db.Patients.Any(p => p.Id == x.Ticket.PatientId && (p.NameAr.Contains(text) || p.NameEn != null && p.NameEn.Contains(text))));
        if (cursor?.LastDate is { } lastDate) { var id = cursor.LastId; query = query.Where(x => x.Ticket.BusinessDate < lastDate || x.Ticket.BusinessDate == lastDate && x.Ticket.Id.CompareTo(id) < 0); }
        var rows = await query.OrderByDescending(x => x.Ticket.BusinessDate).ThenByDescending(x => x.Ticket.Id).Take(filter.PageSize + 1).ToArrayAsync(ct);
        var items = rows.Take(filter.PageSize).Select(x => new ArchivedTicketItem(x.Ticket.Id, x.Ticket.DoctorPracticeId, x.Ticket.TicketNumber, x.Ticket.BusinessDate,
            x.Ticket.Status.ToString(), x.Registry.ArchivedAtUtc!.Value, actor == ReportActor.Patient ? $"/api/v1/tickets/mine/{x.Ticket.Id}" : actor == ReportActor.SuperAdmin ? $"/api/v1/admin/archives/tickets/{x.Ticket.Id}" : $"/api/v1/practices/{x.Ticket.DoctorPracticeId}/tickets/{x.Ticket.Id}")).ToArray();
        var next = rows.Length > filter.PageSize ? tokens.Encode(new(binding, asOf, null, items[^1].BusinessDate, items[^1].TicketId, Watermark: watermark)) : null;
        return Result<ArchivePage<ArchivedTicketItem>>.Ok(new(items, next, filter.PageSize, asOf));
    }

    public async Task<Result<ArchivePage<ArchivedReservationItem>>> ReservationsAsync(ReportActor actor, Guid? practice, ArchiveFilter filter, CancellationToken ct)
    {
        if (!Valid(filter, actor, false)) return Result<ArchivePage<ArchivedReservationItem>>.Fail(ArchiveErrors.InvalidFilter);
        var scope = await guard.AuthorizeAsync(actor, Permission(actor, false), practice ?? filter.PracticeId, null, ct);
        if (scope.IsFailure) return Result<ArchivePage<ArchivedReservationItem>>.Fail(scope.Errors);
        var patient = scope.Value.PatientId;
        if (actor == ReportActor.Patient && filter.PatientId is { } dependent && dependent != patient)
        { if (!await DependentAsync(scope.Value, dependent, ct)) return Result<ArchivePage<ArchivedReservationItem>>.Fail(ArchiveErrors.NotFound); patient = dependent; }
        var binding = ArchiveTokens.Binding(new { type = "Reservation", actor, filter = filter with { Cursor = null }, practice }, scope.Value.ActorId, scope.Value.PracticeIds, scope.Value.Permissions);
        ArchiveCursor? cursor; try { cursor = tokens.Decode(filter.Cursor, binding); } catch (FormatException) { return Result<ArchivePage<ArchivedReservationItem>>.Fail(ArchiveErrors.InvalidCursor); }
        var watermark = cursor?.Watermark ?? await db.ArchiveProgress.AsNoTracking().Where(x => x.Id == "CommittedSequence").Select(x => x.Sequence).SingleOrDefaultAsync(ct);
        var asOf = cursor?.AsOfUtc ?? clock.UtcNow; var practices = scope.Value.PracticeIds.ToArray();
        var query = from t in db.Set<ArchivedReservation>().AsNoTracking() join r in db.ReservationRegistry.AsNoTracking() on t.Id equals r.Id
            where r.IsArchived && r.ArchiveSequence <= watermark && r.ArchivedAtUtc <= asOf && (actor == ReportActor.Patient ? t.PatientId == patient : practices.Contains(t.DoctorPracticeId))
            select new { Reservation = t, Registry = r, TicketId = db.TicketRegistry.Where(x => x.ReservationId == t.Id).Select(x => (Guid?)x.Id).FirstOrDefault() };
        if (filter.FromBusinessDate != null) query = query.Where(x => x.Reservation.BusinessDate >= filter.FromBusinessDate);
        if (filter.ToBusinessDate != null) query = query.Where(x => x.Reservation.BusinessDate <= filter.ToBusinessDate);
        if (filter.Status != null) { var status = Enum.Parse<ReservationStatus>(filter.Status); query = query.Where(x => x.Reservation.Status == status); }
        if (filter.Reference != null) query = query.Where(x => x.Reservation.ReservationReference.Contains(filter.Reference));
        if (actor != ReportActor.Patient && filter.PatientId != null) query = query.Where(x => x.Reservation.PatientId == filter.PatientId);
        if (filter.Search is { Length: > 0 } text) query = query.Where(x => x.Reservation.ReservationReference.Contains(text) || db.Patients.Any(p => p.Id == x.Reservation.PatientId && (p.NameAr.Contains(text) || p.NameEn != null && p.NameEn.Contains(text))));
        if (cursor?.LastDate is { } lastDate) { var id = cursor.LastId; query = query.Where(x => x.Reservation.BusinessDate < lastDate || x.Reservation.BusinessDate == lastDate && x.Reservation.Id.CompareTo(id) < 0); }
        var rows = await query.OrderByDescending(x => x.Reservation.BusinessDate).ThenByDescending(x => x.Reservation.Id).Take(filter.PageSize + 1).ToArrayAsync(ct);
        var items = rows.Take(filter.PageSize).Select(x => new ArchivedReservationItem(x.Reservation.Id, x.Reservation.DoctorPracticeId, x.Reservation.ReservationReference, x.Reservation.BusinessDate,
            x.Reservation.Status.ToString(), x.Registry.ArchivedAtUtc!.Value, actor switch { ReportActor.Patient => $"/api/v1/reservations/mine/{x.Reservation.Id}", ReportActor.SuperAdmin => $"/api/v1/admin/archives/reservations/{x.Reservation.Id}", ReportActor.Reception => $"/api/v1/reception/practices/{x.Reservation.DoctorPracticeId}/reservations/{x.Reservation.Id}", _ => $"/api/v1/doctors/me/practices/{x.Reservation.DoctorPracticeId}/reservations/{x.Reservation.Id}" }, actor == ReportActor.SuperAdmin ? null : x.TicketId)).ToArray();
        var next = rows.Length > filter.PageSize ? tokens.Encode(new(binding, asOf, null, items[^1].BusinessDate, items[^1].ReservationId, Watermark: watermark)) : null;
        return Result<ArchivePage<ArchivedReservationItem>>.Ok(new(items, next, filter.PageSize, asOf));
    }
    public async Task<Result<ArchivePage<PlatformTicketMetadata>>> PlatformTicketsAsync(ArchiveFilter filter, CancellationToken ct)
    { var result = await TicketsAsync(ReportActor.SuperAdmin, filter.PracticeId, filter, ct); return result.IsFailure ? Result<ArchivePage<PlatformTicketMetadata>>.Fail(result.Errors) : Result<ArchivePage<PlatformTicketMetadata>>.Ok(new(result.Value.Items.Select(x => new PlatformTicketMetadata(x.TicketId,x.DoctorPracticeId,x.TicketNumber,x.BusinessDate,x.Status,x.ArchivedAtUtc,x.DetailsRoute)).ToArray(),result.Value.NextCursor,result.Value.PageSize,result.Value.AsOfUtc)); }
    public async Task<Result<ArchivePage<PlatformReservationMetadata>>> PlatformReservationsAsync(ArchiveFilter filter, CancellationToken ct)
    { var result = await ReservationsAsync(ReportActor.SuperAdmin, filter.PracticeId, filter, ct); return result.IsFailure ? Result<ArchivePage<PlatformReservationMetadata>>.Fail(result.Errors) : Result<ArchivePage<PlatformReservationMetadata>>.Ok(new(result.Value.Items.Select(x => new PlatformReservationMetadata(x.ReservationId,x.DoctorPracticeId,x.ReservationReference,x.BusinessDate,x.Status,x.ArchivedAtUtc,x.DetailsRoute)).ToArray(),result.Value.NextCursor,result.Value.PageSize,result.Value.AsOfUtc)); }
    public async Task<Result<PlatformTicketMetadata>> PlatformTicketAsync(Guid id, CancellationToken ct)
    { var scope = await guard.AuthorizeAsync(ReportActor.SuperAdmin, Phase18PartTwoPermissionNames.TicketsPlatform,null,null,ct); if(scope.IsFailure) return Result<PlatformTicketMetadata>.Fail(scope.Errors); var row = await (from t in db.Set<ArchivedTicket>() join r in db.TicketRegistry on t.Id equals r.Id where t.Id == id && r.IsArchived select new {t,r}).SingleOrDefaultAsync(ct); return row == null ? Result<PlatformTicketMetadata>.Fail(ArchiveErrors.NotFound) : Result<PlatformTicketMetadata>.Ok(new(row.t.Id,row.t.DoctorPracticeId,row.t.TicketNumber,row.t.BusinessDate,row.t.Status.ToString(),row.r.ArchivedAtUtc!.Value,$"/api/v1/admin/archives/tickets/{id}")); }
    public async Task<Result<PlatformReservationMetadata>> PlatformReservationAsync(Guid id, CancellationToken ct)
    { var scope = await guard.AuthorizeAsync(ReportActor.SuperAdmin, Phase18PartTwoPermissionNames.ReservationsPlatform,null,null,ct); if(scope.IsFailure) return Result<PlatformReservationMetadata>.Fail(scope.Errors); var row = await (from t in db.Set<ArchivedReservation>() join r in db.ReservationRegistry on t.Id equals r.Id where t.Id == id && r.IsArchived select new {t,r}).SingleOrDefaultAsync(ct); return row == null ? Result<PlatformReservationMetadata>.Fail(ArchiveErrors.NotFound) : Result<PlatformReservationMetadata>.Ok(new(row.t.Id,row.t.DoctorPracticeId,row.t.ReservationReference,row.t.BusinessDate,row.t.Status.ToString(),row.r.ArchivedAtUtc!.Value,$"/api/v1/admin/archives/reservations/{id}")); }

    public async Task<Result<OptionsPage<PracticeOption>>> PracticesAsync(ReportActor actor, string? search, int pageNumber, int pageSize, CancellationToken ct)
    {
        if (pageNumber is < 1 or > 100000 || pageSize is < 1 or > 100 || search?.Length > 100) return Result<OptionsPage<PracticeOption>>.Fail(ArchiveErrors.InvalidFilter);
        var access = user.UserId is { } actorId ? await store.GetAccessSnapshotAsync(actorId,ct) : null;
        var allowed = actor == ReportActor.Doctor ? Phase18PartTwoPermissionNames.DoctorDefaults : actor == ReportActor.Reception ? Phase18PartTwoPermissionNames.ReceptionDefaults : new[] {Phase18PartTwoPermissionNames.TicketsPlatform,Phase18PartTwoPermissionNames.ReservationsPlatform,Phase18PartTwoPermissionNames.Failures};
        var permissions = allowed.Where(x => access?.Permissions.Contains(x) == true).ToArray();
        if (permissions.Length == 0) return Result<OptionsPage<PracticeOption>>.Fail(ArchiveErrors.Denied);
        var readable = new HashSet<Guid>(); var authorized = false;
        foreach (var permission in permissions)
        {
            var scope = await guard.AuthorizeAsync(actor,permission,null,null,ct);
            if (scope.IsFailure) continue;
            authorized = true; readable.UnionWith(scope.Value.PracticeIds);
        }
        if (!authorized) return Result<OptionsPage<PracticeOption>>.Fail(ArchiveErrors.Denied);
        var ids=readable.ToArray(); var query = db.DoctorPractices.AsNoTracking().Where(x=>ids.Contains(x.Id));
        if (search is {Length: >0}) query=query.Where(x=>x.NameAr.Contains(search)||x.NameEn!=null&&x.NameEn.Contains(search));
        var total=await query.LongCountAsync(ct); var items=await query.OrderBy(x=>x.NameAr).ThenBy(x=>x.Id).Skip((pageNumber-1)*pageSize).Take(pageSize).Select(x=>new PracticeOption(x.Id,x.NameAr,x.NameEn)).ToArrayAsync(ct);
        return Result<OptionsPage<PracticeOption>>.Ok(new(items,pageNumber,pageSize,total));
    }
}

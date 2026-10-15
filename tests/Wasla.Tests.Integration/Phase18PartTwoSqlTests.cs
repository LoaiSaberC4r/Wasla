#pragma warning disable EF1002 // Trigger identifiers are fixed test constants, never request data.
using System.Net;
using ClosedXML.Excel;
using Wasla.Application.Features.Reporting;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using System.Collections;
using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Domain.Archives;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;
using Wasla.Domain.Payments;
using Wasla.Domain.Practices;
using Wasla.Application.Features.Archives;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase18PartTwoSqlTests
{
    private static void Enable(Phase13ApiFixture app)
    {
        var flags = app.Services.GetRequiredService<IOptions<ArchiveOptions>>().Value;
        flags.Enabled = true; flags.ReadModelsVerified = true;
    }
    private static async Task<ArchiveRun?> Process(Phase13ApiFixture app, DbCommandInterceptor? interceptor = null)
    {
        using var scope = app.Services.CreateScope();
        if(interceptor==null)return await scope.ServiceProvider.GetRequiredService<ArchiveEngine>().ProcessAsync(TestContext.Current.CancellationToken);
        var registered=scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
        await using var db=new WaslaDbContext(new DbContextOptionsBuilder<WaslaDbContext>().UseSqlServer(registered.Database.GetConnectionString()).AddInterceptors(interceptor).Options,app.Clock);
        var lockType=typeof(ArchiveEngine).Assembly.GetType("Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.TicketQueueLock")!;
        var locks=(Wasla.Application.Features.Tickets.Common.ITicketQueueLock)ActivatorUtilities.CreateInstance(scope.ServiceProvider,lockType,db);
        return await new ArchiveEngine(db,locks,app.Clock,scope.ServiceProvider.GetRequiredService<IOptions<ArchiveOptions>>()).ProcessAsync(TestContext.Current.CancellationToken);
    }
    private static async Task Close(Phase13ApiFixture app)
    {
        app.Clock.UtcNow = app.Clock.UtcNow.AddDays(1);
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ArchiveOperationalDays>().ProcessAsync(TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task Linked_group_rolls_back_each_database_step_then_replays_and_refunds_without_unarchiving()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true); using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception"); using var patient = await app.ClientAsync("patient"); using var admin = await app.ClientAsync("root");
        var booking = new { patientId=app.PatientId, doctorPracticeId=app.PracticeId, businessDate=app.Today, slotStartTime="18:00", segmentId=app.SegmentId, visitTypeId=app.ConsultationTypeId, bookingNote="Synthetic private note" };
        var bookingKey=Guid.NewGuid().ToString("N"); using var booked=await PostAsync(patient,"/api/v1/reservations",booking,bookingKey); Assert.Equal(HttpStatusCode.Created,booked.StatusCode);
        var reservation=await JsonAsync(booked); var reservationId=reservation.GetProperty("reservationId").GetGuid();
        app.Clock.UtcNow=app.Clock.UtcNow.Date.AddHours(14).AddMinutes(40);
        var admissionBody=new {paidAmount=300,paymentMethod="Cash"}; var admissionKey=Guid.NewGuid().ToString("N"); var checkIn=$"/api/v1/practices/{app.PracticeId}/reservations/{reservationId}/check-in";
        using var admitted=await PostAsync(reception,checkIn,admissionBody,admissionKey);Assert.Equal(HttpStatusCode.Created,admitted.StatusCode);var ticket=await JsonAsync(admitted);var id=ticket.GetProperty("ticketId").GetGuid();
        using var call=await PostAsync(doctor,$"/api/v1/practices/{app.PracticeId}/queue/call-next",new{});Assert.Equal(HttpStatusCode.OK,call.StatusCode);ticket=await GetAsync(doctor,app.TicketUrl(id));
        var cancelBody=new {reason="Synthetic cancellation",rowVersion=ticket.GetProperty("rowVersion").GetString()};var cancelKey=Guid.NewGuid().ToString("N");
        using var canceled=await PostAsync(doctor,app.TicketUrl(id)+"/cancel",cancelBody,cancelKey);Assert.Equal(HttpStatusCode.OK,canceled.StatusCode);
        await Close(app);Enable(app);
        foreach(var point in new[]{("archive.Reservations","INSERT","1=1"),("archive.ReservationHistories","INSERT","1=1"),("archive.Tickets","INSERT","1=1"),("archive.TicketHistories","INSERT","1=1"),("archive.TicketCallAttempts","INSERT","1=1"),
            ("dbo.ReservationHistories","DELETE","1=1"),("dbo.TicketHistories","DELETE","1=1"),("dbo.TicketCallAttempts","DELETE","1=1"),("dbo.Tickets","DELETE","1=1"),("dbo.Reservations","DELETE","1=1"),
            ("dbo.TicketRegistry","UPDATE","1=1"),("dbo.ReservationRegistry","UPDATE","1=1"),("dbo.ArchiveAuditEvents","INSERT","EXISTS(SELECT 1 FROM inserted WHERE Action=N'TicketArchived')"),
            ("dbo.ArchiveRuns","INTERCEPT",""),("dbo.ArchiveProgress","INTERCEPT","")})
        {
            var trigger=point.Item1.Split('.')[0]+".PartTwoFailure";
            // SQL Server forbids EF OUTPUT on a rowversion table with triggers, even for unrelated inserts.
            // Intercept its transactional UPDATE so the intended movement checkpoint is exercised.
            var fault=point.Item2=="INTERCEPT"?new MovementCheckpointFailure(point.Item1):null;
            if(fault==null)await app.WithDbAsync(db=>db.Database.ExecuteSqlRawAsync($"CREATE TRIGGER {trigger} ON {point.Item1} AFTER {point.Item2} AS IF {point.Item3} THROW 51001,'Synthetic failure',1;",TestContext.Current.CancellationToken));
            try
            {
                ArchiveRun? failed=null;for(var attempt=0;attempt<4&&(failed==null||failed.Scanned==0);attempt++)failed=await Process(app,fault);
                if(fault!=null)Assert.True(fault.Fired);
                Assert.NotNull(failed); Assert.Equal(1,failed.Excluded);Assert.Equal(0,failed.ArchivedTickets); Assert.Equal(0,failed.ArchivedReservations);
                await app.WithDbAsync(async db=>
                {
                    Assert.True(await db.Tickets.AnyAsync(x=>x.Id==id));Assert.True(await db.Reservations.AnyAsync(x=>x.Id==reservationId));
                    Assert.False(await db.Set<ArchivedTicket>().AnyAsync(x=>x.Id==id));Assert.False(await db.Set<ArchivedReservation>().AnyAsync(x=>x.Id==reservationId));
                    Assert.False(await db.TicketRegistry.Where(x=>x.Id==id).Select(x=>x.IsArchived).SingleAsync());
                });
            }
            finally { if(fault==null)await app.WithDbAsync(db=>db.Database.ExecuteSqlRawAsync($"DROP TRIGGER {trigger}")); }
        }
        ArchiveRun? moved=null;for(var cycle=0;cycle<5&&(moved==null||moved.ArchivedTickets==0);cycle++)moved=await Process(app);Assert.Equal(1,moved!.ArchivedTickets);Assert.Equal(1,moved.ArchivedReservations);
        var own=await GetAsync(patient,$"/api/v1/reservations/mine/{reservationId}");Assert.True(own.GetProperty("isArchived").GetBoolean());Assert.Equal(id,own.GetProperty("linkedTicketId").GetGuid());Assert.True(own.GetProperty("canViewLinkedTicket").GetBoolean());
        var staff=await GetAsync(reception,$"/api/v1/reception/practices/{app.PracticeId}/reservations/{reservationId}");Assert.True(staff.GetProperty("isArchived").GetBoolean());
        var safe=await GetAsync(admin,$"/api/v1/admin/archives/reservations/{reservationId}");Assert.False(safe.TryGetProperty("patient",out _));Assert.False(safe.TryGetProperty("bookingNote",out _));
        using var retryBooking=await PostAsync(patient,"/api/v1/reservations",booking,bookingKey);Assert.Equal(HttpStatusCode.Created,retryBooking.StatusCode);Assert.Equal(reservationId,(await JsonAsync(retryBooking)).GetProperty("reservationId").GetGuid());
        using var retryAdmission=await PostAsync(reception,checkIn,admissionBody,admissionKey);Assert.Equal(HttpStatusCode.Created,retryAdmission.StatusCode);
        using var retryCancel=await PostAsync(doctor,app.TicketUrl(id)+"/cancel",cancelBody,cancelKey);Assert.Equal(HttpStatusCode.OK,retryCancel.StatusCode);
        using var changed=await PostAsync(doctor,app.TicketUrl(id)+"/cancel",new {reason="Changed payload",rowVersion=cancelBody.rowVersion},cancelKey);Assert.Equal(HttpStatusCode.Conflict,changed.StatusCode);
        var detail=await GetAsync(doctor,app.TicketUrl(id));var paymentId=detail.GetProperty("paymentId").GetGuid();
        using var refund=await PostAsync(doctor,$"/api/v1/practices/{app.PracticeId}/payments/{paymentId}/refund",new{refundMethod="Cash",refundReasonCode="PatientRequestedCancellation"});Assert.Equal(HttpStatusCode.OK,refund.StatusCode);
        using var duplicate=await PostAsync(doctor,$"/api/v1/practices/{app.PracticeId}/payments/{paymentId}/refund",new{refundMethod="Cash",refundReasonCode="PatientRequestedCancellation"});Assert.Equal(HttpStatusCode.Conflict,duplicate.StatusCode);
        await app.WithDbAsync(async db=>{Assert.Single(await db.Refunds.Where(x=>x.TicketId==id).ToArrayAsync());Assert.False(await db.Tickets.AnyAsync(x=>x.Id==id));Assert.Single(await db.ArchiveIntegrityIssues.Where(x=>x.EntityId==id).ToArrayAsync());});
    }
    [Fact]
    public async Task Readiness_rejects_missing_fk_and_two_workers_move_once_with_snapshot_pagination_and_revocation()
    {
        await using var app=await Phase13ApiFixture.CreateAsync(true);using var reception=await app.ClientAsync("reception");using var doctor=await app.ClientAsync("doctor");
        for(var i=0;i<3;i++){var t=await app.WalkInAsync(reception);using var c=await PostAsync(doctor,app.TicketUrl(t.GetProperty("ticketId").GetGuid())+"/cancel",new{reason="Synthetic",rowVersion=t.GetProperty("rowVersion").GetString()});Assert.Equal(HttpStatusCode.OK,c.StatusCode);}
        await Close(app);Enable(app);
        await app.WithDbAsync(db=>db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.Payments DROP CONSTRAINT FK_Payments_TicketRegistry_TicketId"));
        Assert.Null(await Process(app));
        await app.WithDbAsync(db=>db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.Payments WITH CHECK ADD CONSTRAINT FK_Payments_TicketRegistry_TicketId FOREIGN KEY (TicketId) REFERENCES dbo.TicketRegistry(Id)"));
        await app.WithDbAsync(db=>db.Database.ExecuteSqlRawAsync("DROP INDEX IX_TicketRegistry_DoctorPracticeId_BusinessDate_TicketNumber ON dbo.TicketRegistry; CREATE UNIQUE INDEX IX_TicketRegistry_DoctorPracticeId_BusinessDate_TicketNumber ON dbo.TicketRegistry(Id)"));Assert.Null(await Process(app));
        await app.WithDbAsync(db=>db.Database.ExecuteSqlRawAsync("DROP INDEX IX_TicketRegistry_DoctorPracticeId_BusinessDate_TicketNumber ON dbo.TicketRegistry; CREATE UNIQUE INDEX IX_TicketRegistry_DoctorPracticeId_BusinessDate_TicketNumber ON dbo.TicketRegistry(DoctorPracticeId,BusinessDate,TicketNumber)"));
        await app.WithDbAsync(db=>db.Database.ExecuteSqlRawAsync("ALTER TABLE archive.Tickets DROP CONSTRAINT CK_ArchiveTickets_Terminal"));Assert.Null(await Process(app));
        await app.WithDbAsync(db=>db.Database.ExecuteSqlRawAsync("ALTER TABLE archive.Tickets WITH CHECK ADD CONSTRAINT CK_ArchiveTickets_Terminal CHECK (Status IN (4,5,6) AND IsOperationallyOpen=0)"));
        var results=await Task.WhenAll(Process(app),Process(app));Assert.Equal(3,results.Where(x=>x!=null).Sum(x=>x!.ArchivedTickets));
        var url=$"/api/v1/doctors/me/practices/{app.PracticeId}/archives/tickets?pageSize=1";var first=await GetAsync(doctor,url);var cursor=first.GetProperty("nextCursor").GetString();Assert.NotNull(cursor);
        await app.WithDbAsync(async db=>
        {
            var at=app.Clock.UtcNow.AddDays(-1);var date=app.Today.AddDays(-1);
            var later=Ticket.CreateWalkIn(new(Guid.NewGuid(),app.DoctorId,app.PracticeId,app.PatientId,null,date,1000,TicketSource.WalkIn,app.SegmentId,"Synthetic","Synthetic",0,app.ConsultationTypeId,"NewConsultation","Synthetic","Synthetic",300,"Africa/Cairo",at,at,CheckInMode.WalkIn,app.DoctorUserId,null)).Value;
            later.Cancel(app.DoctorUserId,"Late discovered historical record",at);db.Tickets.Add(later);
            db.Payments.Add(Payment.RecordPaid(new(Guid.NewGuid(),app.DoctorId,app.PracticeId,app.PatientId,null,later.Id,300,300,"PAY-SYNTHETIC-LATE",1000,date,PaymentMethod.Cash,null,null,app.DoctorUserId,at)).Value);await db.SaveChangesAsync();
        });
        await Process(app);await Process(app);
        var seen=new HashSet<Guid>{first.GetProperty("items")[0].GetProperty("ticketId").GetGuid()};
        while(cursor!=null){var page=await GetAsync(doctor,url+"&cursor="+Uri.EscapeDataString(cursor));foreach(var item in page.GetProperty("items").EnumerateArray())Assert.True(seen.Add(item.GetProperty("ticketId").GetGuid()));cursor=page.GetProperty("nextCursor").GetString();}Assert.Equal(3,seen.Count);
        using var tampered=await doctor.GetAsync(url+"&cursor=invalid",TestContext.Current.CancellationToken);Assert.Equal(HttpStatusCode.UnprocessableEntity,tampered.StatusCode);
        var firstCursor=first.GetProperty("nextCursor").GetString()!;using var altered=await doctor.GetAsync(url+"&status=Cancelled&cursor="+Uri.EscapeDataString(firstCursor),TestContext.Current.CancellationToken);Assert.Equal(HttpStatusCode.UnprocessableEntity,altered.StatusCode);
        await app.WithDbAsync(db=>db.RolePermissions.Where(x=>x.RoleId==SystemRoleIds.Doctor&&x.PermissionId==SystemPermissionIds.For(Phase18PartTwoPermissionNames.TicketsPractice)).ExecuteDeleteAsync());
        using var revoked=await doctor.GetAsync(url,TestContext.Current.CancellationToken);Assert.Equal(HttpStatusCode.Forbidden,revoked.StatusCode);
    }
    [Fact]
    public async Task Completed_encounter_stays_readable_while_inprogress_sibling_survives_and_reports_keep_asof()
    {
        await using var app=await Phase13ApiFixture.CreateAsync(true);using var doctor=await app.ClientAsync("doctor");using var reception=await app.ClientAsync("reception");
        var completed=await app.CompleteAsync(doctor,await app.StartAsync(doctor,await app.WalkInAsync(reception)));var id=completed.Ticket.GetProperty("ticketId").GetGuid();var encounterId=completed.Ticket.GetProperty("medicalEncounterId").GetGuid();
        var active=await app.StartAsync(doctor,await app.WalkInAsync(reception,patientId:app.OtherPatientId));var activeId=active.GetProperty("ticketId").GetGuid();
        app.Clock.UtcNow=app.Clock.UtcNow.AddMinutes(2);
        var date=app.Today;var asOf=Uri.EscapeDataString(app.Clock.UtcNow.AddMinutes(-1).ToString("O"));var query=$"?practiceId={app.PracticeId}&fromDate={date:yyyy-MM-dd}&toDate={date:yyyy-MM-dd}&asOfUtc={asOf}";
        using var platformActor=await app.ClientAsync("root");
        var actors=new[]{(Kind:ReportActor.Doctor,Client:doctor,Prefix:"/api/v1/doctors/me/reports/"),(Kind:ReportActor.Reception,Client:reception,Prefix:"/api/v1/reception/reports/"),(Kind:ReportActor.SuperAdmin,Client:platformActor,Prefix:"/api/v1/admin/reports/")};
        var cases=actors.SelectMany(actor=>ReportDefinitionCatalog.Definitions.Where(x=>x.Actors.Contains(actor.Kind)).Select(x=>(actor.Client,Url:(x.Code=="overview"?actor.Prefix.Replace("reports/","dashboard/",StringComparison.Ordinal):actor.Prefix)+x.Code+query))).ToArray();
        var before=new Dictionary<string,JsonElement>();foreach(var item in cases)before[item.Url]=await GetAsync(item.Client,item.Url);
        async Task<string[]> Workbook(string language)
        {
            using var response=await doctor.GetAsync("/api/v1/doctors/me/reports/queue/export"+query+"&format=xlsx&language="+language,TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            using var bytes=new MemoryStream(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));using var book=new XLWorkbook(bytes);
            Assert.Equal(language=="ar",book.Worksheet(1).RightToLeft);
            // Generated UTC is processing metadata. All source facts and AsOf UTC must remain identical.
            return book.Worksheets.SelectMany(sheet=>sheet.CellsUsed().Where(cell=>sheet.Name!="01 Summary"||cell.Address.RowNumber!=9)
                .Select(cell=>sheet.Name+":"+cell.Address+":"+cell.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray();
        }
        var chromium=Environment.GetEnvironmentVariable("WASLA_CHROMIUM_PATH");
        if(!string.IsNullOrWhiteSpace(chromium))app.Services.GetRequiredService<IOptions<Phase18Options>>().Value.ChromiumExecutablePath=chromium;
        async Task Pdf(string language)
        {
            if(string.IsNullOrWhiteSpace(chromium))return; // Dedicated renderer tests report the missing browser gate separately.
            using var response=await doctor.GetAsync("/api/v1/doctors/me/reports/queue/export"+query+"&format=pdf&language="+language,TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.Equal("application/pdf",response.Content.Headers.ContentType?.MediaType);
            Assert.True((await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).AsSpan().StartsWith("%PDF"u8));
        }
        var enBefore=await Workbook("en");var arBefore=await Workbook("ar");await Pdf("en");await Pdf("ar");
        await Close(app);Enable(app);var run=await Process(app);Assert.Equal(1,run!.ArchivedTickets);
        foreach(var item in cases)
        {
            var after=await GetAsync(item.Client,item.Url);
            foreach(var field in new[]{"metrics","series","breakdowns","dataStatus","eligibleSampleCount","excludedSampleCount","detailRowCount"})
                Assert.True(before[item.Url].GetProperty(field).GetRawText()==after.GetProperty(field).GetRawText(),item.Url+" "+field+" changed after relocation");
        }
        Assert.Equal(enBefore,await Workbook("en"));Assert.Equal(arBefore,await Workbook("ar"));await Pdf("en");await Pdf("ar");
        var encounter=await GetAsync(doctor,app.EncounterUrl(encounterId));
        using var amendment=await PostAsync(doctor,app.EncounterUrl(encounterId)+"/amendments",new{reason="Synthetic reviewed amendment",encounterRowVersion=encounter.GetProperty("rowVersion").GetString(),changes=new[]{new{target="ClinicalNotes",changeType="Updated",clinicalNotes="Synthetic corrected documentation"}}});
        Assert.Equal(HttpStatusCode.Created,amendment.StatusCode);
        await app.WithDbAsync(async db=>{Assert.True(await db.Tickets.AnyAsync(x=>x.Id==activeId&&x.Status==TicketStatus.InProgress));Assert.True(await db.MedicalEncounters.AnyAsync(x=>x.Id==encounterId));});
        var details=await GetAsync(doctor,app.TicketUrl(id));Assert.True(details.GetProperty("isArchived").GetBoolean());Assert.Equal(encounterId,details.GetProperty("medicalEncounterId").GetGuid());
        var late=await app.CompleteAsync(doctor,active);Assert.Equal("Completed",late.Ticket.GetProperty("status").GetString());
        await app.WithDbAsync(db=>db.RolePermissions.Where(x=>x.RoleId==SystemRoleIds.Doctor&&x.PermissionId==SystemPermissionIds.For(PermissionNames.MedicalEncountersViewOwn)).ExecuteDeleteAsync());
        details=await GetAsync(doctor,app.TicketUrl(id));Assert.Equal(JsonValueKind.Null,details.GetProperty("medicalEncounterId").ValueKind);
    }
    [Fact]
    public async Task Closed_noshow_keeps_status_releases_patient_lease_and_missing_payment_is_quarantined()
    {
        await using var app=await Phase13ApiFixture.CreateAsync(true);using var doctor=await app.ClientAsync("doctor");using var reception=await app.ClientAsync("reception");using var admin=await app.ClientAsync("root");
        var ticket=await app.WalkInAsync(reception);var id=ticket.GetProperty("ticketId").GetGuid();
        async Task NoShow()
        {
            await app.WithDbAsync(async db=>{var row=await db.Tickets.Include(x=>x.History).Include(x=>x.CallAttempts).SingleAsync(x=>x.Id==id);Assert.True(row.Call(app.DoctorUserId,app.Clock.UtcNow).IsSuccess);for(var i=0;i<3;i++){Assert.True(row.ConfirmNoResponse(app.DoctorUserId,3,app.Clock.UtcNow).IsSuccess);if(i<2)Assert.True(row.Recall(app.DoctorUserId,3,app.Clock.UtcNow).IsSuccess);}await db.SaveChangesAsync();});
        }
        await NoShow();ticket=await GetAsync(doctor,app.TicketUrl(id));using var restore=await PostAsync(reception,app.TicketUrl(id)+"/restore-no-show",new{rowVersion=ticket.GetProperty("rowVersion").GetString()});Assert.Equal(HttpStatusCode.OK,restore.StatusCode);await NoShow();
        await Close(app);ticket=await GetAsync(doctor,app.TicketUrl(id));Assert.Equal("NoShow",ticket.GetProperty("status").GetString());Assert.False(ticket.GetProperty("canRefund").GetBoolean());Assert.False(ticket.GetProperty("operationalCapabilities").GetProperty("canRestore").GetBoolean());
        foreach(var operation in new[]{"restore-no-show","cancel"}){using var blocked=await PostAsync(operation=="cancel"?doctor:reception,app.TicketUrl(id)+"/"+operation,new{reason="Forbidden closed reclassification",rowVersion=ticket.GetProperty("rowVersion").GetString()});Assert.Equal(HttpStatusCode.Conflict,blocked.StatusCode);}
        var returned=await app.WalkInAsync(reception);Assert.NotEqual(id,returned.GetProperty("ticketId").GetGuid());
        await app.WithDbAsync(db=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.Payments WHERE TicketId={id}"));Enable(app);await Process(app);await Process(app);await Process(app);
        var exceptions=await GetAsync(admin,"/api/v1/admin/archives/operations/exceptions?failureCode=MissingPayment");Assert.Single(exceptions.GetProperty("items").EnumerateArray());var exceptionId=exceptions.GetProperty("items")[0].GetProperty("exceptionId").GetGuid();await GetAsync(admin,$"/api/v1/admin/archives/operations/exceptions/{exceptionId}");
        await app.WithDbAsync(async db=>{Assert.True(await db.Tickets.AnyAsync(x=>x.Id==id&&x.Status==TicketStatus.NoShow&&!x.IsOperationallyOpen));Assert.False(await db.Set<ArchivedTicket>().AnyAsync(x=>x.Id==id));Assert.Single(await db.ArchiveIntegrityIssues.Where(x=>x.EntityId==id).ToArrayAsync());});
    }
    [Fact]
    public async Task Reservation_only_day_tracks_effective_schedule_invalidates_evidence_and_moves_independent_history()
    {
        await using var app=await Phase13ApiFixture.CreateAsync(true);using var patient=await app.ClientAsync("patient");using var other=await app.ClientAsync("other-patient");using var doctor=await app.ClientAsync("doctor");using var admin=await app.ClientAsync("root");
        var date=app.Today;
        async Task<JsonElement> Book(HttpClient client,Guid subject,string time)
        {
            using var response=await PostAsync(client,"/api/v1/reservations",new{patientId=subject,doctorPracticeId=app.PracticeId,businessDate=date,slotStartTime=time,segmentId=app.SegmentId,visitTypeId=app.ConsultationTypeId});
            Assert.Equal(HttpStatusCode.Created,response.StatusCode);return await JsonAsync(response);
        }
        var cancelled=await Book(patient,app.PatientId,"18:00");var expired=await Book(other,app.OtherPatientId,"19:00");var cancelledId=cancelled.GetProperty("reservationId").GetGuid();var expiredId=expired.GetProperty("reservationId").GetGuid();
        using var cancel=await PostAsync(patient,$"/api/v1/reservations/mine/{cancelledId}/cancel",new{reasonCode="PatientChangedPlans",rowVersion=cancelled.GetProperty("rowVersion").GetString()});Assert.Equal(HttpStatusCode.OK,cancel.StatusCode);
        DateTime revisedEnd=default;Guid exceptionId=default;
        await app.WithDbAsync(async db=>
        {
            Assert.Empty(await db.Tickets.ToArrayAsync());
            var period=await db.DoctorPracticeSchedulePeriods.SingleAsync(x=>x.DoctorPracticeId==app.PracticeId&&x.DayOfWeek==date.DayOfWeek);
            Assert.True(period.Update(date.DayOfWeek,new(8,0),new(13,0),20,app.DoctorUserId).IsSuccess);
            db.DoctorPracticeSchedulePeriods.Add(DoctorPracticeSchedulePeriod.Create(Guid.NewGuid(),app.PracticeId,date.DayOfWeek,new(14,0),new(22,0),20,app.DoctorUserId).Value);await db.SaveChangesAsync();
            var day=await db.PracticeOperationalDays.SingleAsync(x=>x.DoctorPracticeId==app.PracticeId&&x.BusinessDate==date);revisedEnd=day.EndsAtUtc;
            Assert.Equal(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new(22,0)),TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo")),revisedEnd);Assert.True(day.Revision>=2);
            var exception=DoctorPracticeScheduleException.Create(Guid.NewGuid(),app.PracticeId,date,DoctorPracticeScheduleExceptionType.DayOff,null,null,null,app.DoctorUserId).Value;exceptionId=exception.Id;db.DoctorPracticeScheduleExceptions.Add(exception);await db.SaveChangesAsync();Assert.False(day.EvidenceVerified);
        });
        Enable(app);Assert.Equal(0,(await Process(app))!.ArchivedReservations);
        await app.WithDbAsync(async db=>
        {
            var exception=await db.DoctorPracticeScheduleExceptions.SingleAsync(x=>x.Id==exceptionId);Assert.True(exception.Update(date,DoctorPracticeScheduleExceptionType.CustomWorkingHours,new(14,0),new(22,0),20,app.DoctorUserId).IsSuccess);await db.SaveChangesAsync();
            Assert.True((await db.PracticeOperationalDays.SingleAsync(x=>x.DoctorPracticeId==app.PracticeId&&x.BusinessDate==date)).EvidenceVerified);
        });
        await Close(app);
        var total=0;for(var cycle=0;cycle<4&&total<2;cycle++)total+=(await Process(app))?.ArchivedReservations??0;Assert.Equal(2,total);
        await app.WithDbAsync(async db=>
        {
            var day=await db.PracticeOperationalDays.SingleAsync(x=>x.DoctorPracticeId==app.PracticeId&&x.BusinessDate==date);Assert.NotNull(day.ClosedAtUtc);Assert.Equal(revisedEnd,day.EndsAtUtc);var revision=day.Revision;
            var exception=await db.DoctorPracticeScheduleExceptions.SingleAsync(x=>x.Id==exceptionId);exception.Update(date,DoctorPracticeScheduleExceptionType.CustomWorkingHours,new(14,0),new(23,0),20,app.DoctorUserId);(await db.DoctorPractices.SingleAsync(x=>x.Id==app.PracticeId)).Deactivate(app.DoctorUserId);await db.SaveChangesAsync();
            Assert.Equal(revisedEnd,day.EndsAtUtc);Assert.Equal(revision,day.Revision);Assert.Empty(await db.Tickets.ToArrayAsync());
            Assert.Equal(2,await db.Set<ArchivedReservation>().CountAsync(x=>x.Id==cancelledId||x.Id==expiredId));
        });
        var rows=await GetAsync(doctor,$"/api/v1/doctors/me/practices/{app.PracticeId}/archives/reservations");Assert.Equal(2,rows.GetProperty("items").GetArrayLength());
        foreach(var row in rows.GetProperty("items").EnumerateArray())await GetAsync(doctor,row.GetProperty("detailsRoute").GetString()!);
        var mine=await GetAsync(patient,"/api/v1/reservations/mine?view=History");Assert.Contains(mine.GetProperty("items").EnumerateArray(),x=>x.GetProperty("reservationId").GetGuid()==cancelledId);
        var platform=await GetAsync(admin,$"/api/v1/admin/archives/reservations?practiceId={app.PracticeId}");foreach(var row in platform.GetProperty("items").EnumerateArray()){var safe=await GetAsync(admin,row.GetProperty("detailsRoute").GetString()!);Assert.False(safe.TryGetProperty("patientId",out _));}
    }
    [Fact]
    public async Task Movement_between_location_check_and_detail_read_cannot_use_a_live_only_grant()
    {
        await using var app=await Phase13ApiFixture.CreateAsync(true);using var doctor=await app.ClientAsync("doctor");using var reception=await app.ClientAsync("reception");using var patient=await app.ClientAsync("patient");
        var ticket=await app.WalkInAsync(reception);var id=ticket.GetProperty("ticketId").GetGuid();using var cancelled=await PostAsync(doctor,app.TicketUrl(id)+"/cancel",new{reason="Synthetic",rowVersion=ticket.GetProperty("rowVersion").GetString()});Assert.Equal(HttpStatusCode.OK,cancelled.StatusCode);
        await Close(app);Enable(app);await Process(app);
        await app.WithDbAsync(db=>db.RolePermissions.Where(x=>x.RoleId==SystemRoleIds.Doctor&&x.PermissionId==SystemPermissionIds.For(Phase18PartTwoPermissionNames.TicketsPractice)||x.RoleId==SystemRoleIds.Patient&&x.PermissionId==SystemPermissionIds.For(Phase18PartTwoPermissionNames.TicketsOwn)).ExecuteDeleteAsync());
        await using var racing=app.WithWebHostBuilder(builder=>builder.ConfigureServices(services=>
        {
            var original=services.Single(x=>x.ServiceType==typeof(IArchiveLocationResolver));services.Remove(original);
            services.AddScoped<IArchiveLocationResolver>(sp=>new StaleFirstLocation((IArchiveLocationResolver)ActivatorUtilities.CreateInstance(sp,original.ImplementationType!)));
        }));
        foreach(var actor in new[]{doctor,patient})
        {
            using var client=racing.CreateClient();client.DefaultRequestHeaders.Authorization=actor.DefaultRequestHeaders.Authorization;
            using var response=await client.GetAsync(actor==doctor?app.TicketUrl(id):$"/api/v1/tickets/mine/{id}",TestContext.Current.CancellationToken);Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
        }
    }
    [Theory]
    [InlineData(1205)]
    [InlineData(1222)]
    public async Task Transient_sql_errors_retry_boundedly_without_partial_moves_or_false_integrity_issues(int error)
    {
        var ct=TestContext.Current.CancellationToken;
        await using var app=await Phase13ApiFixture.CreateAsync(true);using var doctor=await app.ClientAsync("doctor");using var reception=await app.ClientAsync("reception");
        var ticket=await app.WalkInAsync(reception);var id=ticket.GetProperty("ticketId").GetGuid();using var canceled=await PostAsync(doctor,app.TicketUrl(id)+"/cancel",new{reason="Synthetic",rowVersion=ticket.GetProperty("rowVersion").GetString()});Assert.Equal(HttpStatusCode.OK,canceled.StatusCode);await Close(app);Enable(app);
        string? connectionString=null;await app.WithDbAsync(db=>{connectionString=db.Database.GetConnectionString();return Task.CompletedTask;});
        await using var blocker=new SqlConnection(connectionString);await blocker.OpenAsync(ct);await using var transaction=(SqlTransaction)await blocker.BeginTransactionAsync(ct);
        using(var hold=blocker.CreateCommand())
        {
            hold.Transaction=transaction;hold.CommandText="SET DEADLOCK_PRIORITY HIGH; SELECT * FROM dbo.Payments WITH(XLOCK,HOLDLOCK,ROWLOCK) WHERE TicketId=@ticket";hold.Parameters.AddWithValue("@ticket",id);await hold.ExecuteNonQueryAsync(ct);
        }
        Task? competing=null;var released=false;
        var probe=new SqlRetryProbe(error,async()=>
        {
            if(competing!=null)await competing;
            await transaction.CommitAsync(ct);released=true;
        });
        async Task Compete()
        {
            await probe.PaymentQueryStarted.Task.WaitAsync(TimeSpan.FromSeconds(20),ct);
            using var command=blocker.CreateCommand();command.Transaction=transaction;command.CommandTimeout=20;
            command.CommandText="SELECT * FROM dbo.Tickets WITH(XLOCK,HOLDLOCK,ROWLOCK) WHERE Id=@ticket";command.Parameters.AddWithValue("@ticket",id);await command.ExecuteNonQueryAsync(ct);
        }
        if(error==1205)competing=Compete();
        try
        {
            var run=await Process(app,probe);Assert.NotNull(run);
            Assert.True(run.ArchivedTickets==1,$"Expected one committed move; SQL errors: {string.Join(',',probe.Errors)}; outcome: {run.Outcome}.");
            Assert.Equal(error==1205?1:2,probe.ExpectedErrors);Assert.True(released);Assert.Equal(0,run.Excluded);Assert.Equal("Succeeded",run.Outcome);
            await app.WithDbAsync(async db=>{Assert.False(await db.Tickets.AnyAsync(x=>x.Id==id,ct));Assert.Single(await db.Set<ArchivedTicket>().Where(x=>x.Id==id).ToArrayAsync(ct));Assert.Empty(await db.ArchiveIntegrityIssues.Where(x=>x.EntityId==id).ToArrayAsync(ct));});
        }
        finally
        {
            if(!released)await transaction.RollbackAsync(ct);
            if(competing!=null)try{await competing;}catch(Exception e)when(e is SqlException or TimeoutException or InvalidOperationException){ /* Preserve the original test failure after releasing owned locks. */ }
        }
    }
    [Fact]
    public async Task Lost_commit_acknowledgement_resolves_registry_without_double_counting_or_false_failure()
    {
        await using var app=await Phase13ApiFixture.CreateAsync(true);using var doctor=await app.ClientAsync("doctor");using var reception=await app.ClientAsync("reception");
        var ticket=await app.WalkInAsync(reception);var id=ticket.GetProperty("ticketId").GetGuid();using var canceled=await PostAsync(doctor,app.TicketUrl(id)+"/cancel",new{reason="Synthetic",rowVersion=ticket.GetProperty("rowVersion").GetString()});Assert.Equal(HttpStatusCode.OK,canceled.StatusCode);await Close(app);
        string? connection=null;await app.WithDbAsync(db=>{connection=db.Database.GetConnectionString();return Task.CompletedTask;});
        var lost=new LostAcknowledgement();await using var context=new WaslaDbContext(new DbContextOptionsBuilder<WaslaDbContext>().UseSqlServer(connection).AddInterceptors(lost).Options,app.Clock);
        using var scope=app.Services.CreateScope();var lockType=typeof(ArchiveEngine).Assembly.GetType("Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.TicketQueueLock")!;
        var locks=(Wasla.Application.Features.Tickets.Common.ITicketQueueLock)ActivatorUtilities.CreateInstance(scope.ServiceProvider,lockType,context);
        var engine=new ArchiveEngine(context,locks,app.Clock,Options.Create(new ArchiveOptions{Enabled=true,ReadModelsVerified=true}));var run=await engine.ProcessAsync(TestContext.Current.CancellationToken);
        Assert.True(lost.Fired);Assert.NotNull(run);Assert.Equal(1,run.ArchivedTickets);Assert.Equal(0,run.Excluded);Assert.Equal("Succeeded",run.Outcome);
        await app.WithDbAsync(async db=>{Assert.True(await db.TicketRegistry.AnyAsync(x=>x.Id==id&&x.IsArchived));Assert.Empty(await db.ArchiveIntegrityIssues.Where(x=>x.EntityId==id).ToArrayAsync());});
    }
    private sealed class SqlRetryProbe(int error,Func<Task> release):DbCommandInterceptor
    {
        public TaskCompletionSource PaymentQueryStarted{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<int> Errors{get;}=[];
        public int ExpectedErrors{get;private set;}
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData eventData,InterceptionResult<DbDataReader> result,CancellationToken cancellationToken=default)
        {
            if(command.CommandText.Contains("[Payments]",StringComparison.Ordinal))
            {
                command.CommandText=(error==1205?"SET DEADLOCK_PRIORITY LOW; SET LOCK_TIMEOUT 10000; ":"SET LOCK_TIMEOUT 100; ")+command.CommandText;
                PaymentQueryStarted.TrySetResult();
            }
            return ValueTask.FromResult(result);
        }
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,CommandExecutedEventData eventData,DbDataReader result,CancellationToken cancellationToken=default)
            => ValueTask.FromResult(command.CommandText.Contains("[Payments]",StringComparison.Ordinal)?(DbDataReader)new ObservedSqlReader(result,ObserveAsync):result);
        public override Task CommandFailedAsync(DbCommand command,CommandErrorEventData eventData,CancellationToken cancellationToken=default)
            => eventData.Exception is SqlException sql?ObserveAsync(sql):Task.CompletedTask;
        private async Task ObserveAsync(SqlException sql)
        {
            Errors.AddRange(sql.Errors.Cast<SqlError>().Select(x=>x.Number));
            if(sql.Number==error&&++ExpectedErrors==(error==1205?1:2))await release();
        }
    }
    // SQL can send result metadata before the first row blocks. Observe ReadAsync
    // as well as ExecuteReaderAsync so the independent blocker releases deterministically.
    private sealed class ObservedSqlReader(DbDataReader inner,Func<SqlException,Task> observe):DbDataReader
    {
        public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
        {try{return await inner.ReadAsync(cancellationToken);}catch(SqlException e){await observe(e);throw;}}
        public override object this[int ordinal]=>inner[ordinal];
        public override object this[string name]=>inner[name];
        public override int Depth=>inner.Depth;
        public override int FieldCount=>inner.FieldCount;
        public override bool HasRows=>inner.HasRows;
        public override bool IsClosed=>inner.IsClosed;
        public override int RecordsAffected=>inner.RecordsAffected;
        public override bool GetBoolean(int ordinal)=>inner.GetBoolean(ordinal);
        public override byte GetByte(int ordinal)=>inner.GetByte(ordinal);
        public override long GetBytes(int ordinal,long dataOffset,byte[]? buffer,int bufferOffset,int length)=>inner.GetBytes(ordinal,dataOffset,buffer,bufferOffset,length);
        public override char GetChar(int ordinal)=>inner.GetChar(ordinal);
        public override long GetChars(int ordinal,long dataOffset,char[]? buffer,int bufferOffset,int length)=>inner.GetChars(ordinal,dataOffset,buffer,bufferOffset,length);
        public override string GetDataTypeName(int ordinal)=>inner.GetDataTypeName(ordinal);
        public override DateTime GetDateTime(int ordinal)=>inner.GetDateTime(ordinal);
        public override decimal GetDecimal(int ordinal)=>inner.GetDecimal(ordinal);
        public override double GetDouble(int ordinal)=>inner.GetDouble(ordinal);
        public override Type GetFieldType(int ordinal)=>inner.GetFieldType(ordinal);
        public override float GetFloat(int ordinal)=>inner.GetFloat(ordinal);
        public override Guid GetGuid(int ordinal)=>inner.GetGuid(ordinal);
        public override short GetInt16(int ordinal)=>inner.GetInt16(ordinal);
        public override int GetInt32(int ordinal)=>inner.GetInt32(ordinal);
        public override long GetInt64(int ordinal)=>inner.GetInt64(ordinal);
        public override string GetName(int ordinal)=>inner.GetName(ordinal);
        public override int GetOrdinal(string name)=>inner.GetOrdinal(name);
        public override string GetString(int ordinal)=>inner.GetString(ordinal);
        public override object GetValue(int ordinal)=>inner.GetValue(ordinal);
        public override T GetFieldValue<T>(int ordinal)=>inner.GetFieldValue<T>(ordinal);
        public override Task<T> GetFieldValueAsync<T>(int ordinal,CancellationToken cancellationToken)=>inner.GetFieldValueAsync<T>(ordinal,cancellationToken);
        public override int GetValues(object[] values)=>inner.GetValues(values);
        public override bool IsDBNull(int ordinal)=>inner.IsDBNull(ordinal);
        public override bool NextResult()=>inner.NextResult();
        public override bool Read()=>inner.Read();
        public override IEnumerator GetEnumerator()=>((IEnumerable)inner).GetEnumerator();
        public override DataTable? GetSchemaTable()=>inner.GetSchemaTable();
        public override void Close()=>inner.Close();
        protected override void Dispose(bool disposing){if(disposing)inner.Dispose();base.Dispose(disposing);}
        public override async ValueTask DisposeAsync(){await inner.DisposeAsync();await base.DisposeAsync();}
    }
    private sealed class MovementCheckpointFailure(string table):DbCommandInterceptor
    {
        public bool Fired{get;private set;}
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData eventData,InterceptionResult<DbDataReader> result,CancellationToken cancellationToken=default)
        {
            if(eventData.Context?.Database.CurrentTransaction!=null&&
                (table=="dbo.ArchiveRuns"&&command.CommandText.Contains("UPDATE [ArchiveRuns]",StringComparison.Ordinal)&&eventData.Context.ChangeTracker.Entries<ArchiveRun>().Any(x=>x.Entity.ArchivedTickets>0)||
                 table=="dbo.ArchiveProgress"&&command.CommandText.Contains("[ArchiveProgress]",StringComparison.Ordinal)&&eventData.Context.ChangeTracker.Entries<ArchiveProgress>().Any(x=>x.Entity.Id=="CommittedSequence"&&x.Entity.Sequence>0)))
            {Fired=true;throw new DbUpdateException("Synthetic movement count write failure");}
            return ValueTask.FromResult(result);
        }
    }
    private sealed class LostAcknowledgement:DbTransactionInterceptor
    {
        public bool Fired {get;private set;}
        public override Task TransactionCommittedAsync(DbTransaction transaction,TransactionEndEventData eventData,CancellationToken cancellationToken=default)
        {
            if(!Fired&&eventData.Context?.ChangeTracker.Entries<TicketRegistry>().Any(x=>x.Entity.IsArchived)==true)
            {Fired=true;throw new DbUpdateException("Synthetic lost commit acknowledgement");}
            return Task.CompletedTask;
        }
    }
    private sealed class StaleFirstLocation(IArchiveLocationResolver inner):IArchiveLocationResolver
    {
        private bool first=true;
        public async Task<ArchiveLocation?> TicketAsync(Guid id,CancellationToken ct){var location=await inner.TicketAsync(id,ct);if(first){first=false;return location is null?null:location with{IsArchived=false,ArchivedAtUtc=null};}return location;}
        public Task<ArchiveLocation?> ReservationAsync(Guid id,CancellationToken ct)=>inner.ReservationAsync(id,ct);
        public Task<Guid?> LinkedTicketAsync(Guid id,CancellationToken ct)=>inner.LinkedTicketAsync(id,ct);
        public Task<bool> IsNoShowDayFinalOrUnverifiableAsync(Guid practice,DateOnly date,string zone,CancellationToken ct)=>inner.IsNoShowDayFinalOrUnverifiableAsync(practice,date,zone,ct);
        public Task<Ticket?> ReadTicketForFinanceAsync(Guid id,CancellationToken ct)=>inner.ReadTicketForFinanceAsync(id,ct);
        public Task<bool> EverStartedAsync(Guid id,CancellationToken ct)=>inner.EverStartedAsync(id,ct);
        public Task AuditStaffTicketReadAsync(Guid id,Guid practice,Guid actor,CancellationToken ct)=>inner.AuditStaffTicketReadAsync(id,practice,actor,ct);
    }
}

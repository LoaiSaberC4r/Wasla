using System.Data.Common;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Domain.Archives;
using Wasla.Domain.Payments;
using Wasla.Domain.Reservations;
using Wasla.Domain.Subscriptions;
using Wasla.Domain.Tickets;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class SubscriptionArchiveAuthorizationTests
{
    private static async Task<(Guid Ticket, Guid Reservation)> HistoricalAsync(Phase13ApiFixture app, DateTime at, int number, Guid? patient = null)
    {
        var ticketId = Guid.NewGuid(); var reservationId = Guid.NewGuid();
        await app.WithDbAsync(async db =>
        {
            var subject = patient ?? app.PatientId; var date = DateOnly.FromDateTime(at);
            var reservation = Reservation.Create(new(reservationId, "RES-HARDEN-" + number, app.DoctorId, app.PracticeId, subject,
                app.SegmentId, app.ConsultationTypeId, ReservationBookingSource.Reception, app.DoctorUserId, null,
                at.AddHours(1), DateTime.SpecifyKind(at.AddHours(3), DateTimeKind.Unspecified), date, "Africa/Cairo", 20,
                "Synthetic", "Synthetic", 0, "NewConsultation", "Synthetic", "Synthetic", 300, null, at)).Value;
            Assert.True(reservation.ConvertToTicket(app.DoctorUserId, at.AddMinutes(1)).IsSuccess);
            var ticket = Ticket.CreateFromReservation(new(ticketId, app.DoctorId, app.PracticeId, subject, reservationId, date,
                number, TicketSource.Reservation, app.SegmentId, "Synthetic", "Synthetic", 0, app.ConsultationTypeId,
                "NewConsultation", "Synthetic", "Synthetic", 300, "Africa/Cairo", at.AddMinutes(1), at.AddMinutes(1), CheckInMode.Normal, app.DoctorUserId, null)).Value;
            Assert.True(ticket.Cancel(app.DoctorUserId, "Synthetic history", at.AddMinutes(2)).IsSuccess);
            db.Add(reservation); db.Add(ticket);
            db.Add(Payment.RecordPaid(new(Guid.NewGuid(), app.DoctorId, app.PracticeId, subject, reservationId, ticketId,
                300, 300, "PAY-HARDEN-" + number, number, date, PaymentMethod.Cash, null, null, app.DoctorUserId, at.AddMinutes(1))).Value);
            await db.SaveChangesAsync();
        });
        return (ticketId, reservationId);
    }

    private static async Task ArchiveAsync(Phase13ApiFixture app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ArchiveOptions>>().Value;
        options.Enabled = true; options.ReadModelsVerified = true;
        await scope.ServiceProvider.GetRequiredService<ArchiveOperationalDays>().ProcessAsync(TestContext.Current.CancellationToken);
        var engine = scope.ServiceProvider.GetRequiredService<ArchiveEngine>();
        Assert.True(await engine.ReadyAsync(TestContext.Current.CancellationToken));
        for (var i = 0; i < 3; i++) Assert.NotNull(await engine.ProcessAsync(TestContext.Current.CancellationToken));
        await app.WithDbAsync(async db => { Assert.Empty(await db.Tickets.ToArrayAsync()); Assert.NotEmpty(await db.Set<ArchivedTicket>().ToArrayAsync()); });
    }

    [Fact]
    public async Task Booking_only_denies_ticket_archives_and_details_but_keeps_reservation_patient_and_platform_archive_rights()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true); await SubscriptionTestData.BookingOnlyAsync(app);
        var row = await HistoricalAsync(app, app.Clock.UtcNow.AddDays(-2), 1); await ArchiveAsync(app);
        using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception");
        using var patient = await app.ClientAsync("patient"); using var admin = await app.ClientAsync("root");
        foreach (var url in new[] { $"/api/v1/doctors/me/practices/{app.PracticeId}/archives/tickets", app.TicketUrl(row.Ticket),
            $"/api/v1/reception/practices/{app.PracticeId}/archives/tickets" })
        {
            using var denied = await (url.Contains("reception/", StringComparison.Ordinal) ? reception : doctor).GetAsync(url, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); var body = await denied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            AssertNoProtectedPayload(body, row.Ticket);
        }
        var reservations = await GetAsync(doctor, $"/api/v1/doctors/me/practices/{app.PracticeId}/archives/reservations");
        var item = Assert.Single(reservations.GetProperty("items").EnumerateArray()); Assert.Equal(row.Reservation, item.GetProperty("reservationId").GetGuid());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, item.GetProperty("linkedTicketId").ValueKind);
        var detail = await GetAsync(doctor, item.GetProperty("detailsRoute").GetString()!);
        Assert.False(detail.GetProperty("canViewLinkedTicket").GetBoolean());
        Assert.False(detail.GetProperty("capabilities").GetProperty("canCancel").GetBoolean());
        Assert.Equal("Archive.ReadOnly", detail.GetProperty("capabilities").GetProperty("cancelBlockedReason").GetString());
        await GetAsync(patient, "/api/v1/tickets/mine/archived"); await GetAsync(patient, $"/api/v1/tickets/mine/{row.Ticket}");
        await GetAsync(patient, "/api/v1/reservations/mine/archived");
        var safe = await GetAsync(admin, $"/api/v1/admin/archives/tickets/{row.Ticket}"); Assert.False(safe.TryGetProperty("patientId", out _));
        using var cross = await doctor.GetAsync($"/api/v1/doctors/me/practices/{app.OtherPracticeId}/archives/reservations", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);
        using var assigned = await reception.GetAsync($"/api/v1/reception/practices/{app.OtherPracticeId}/archives/reservations", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, assigned.StatusCode);
        var audit = await GetAsync(doctor, $"/api/v1/doctors/me/practices/{app.PracticeId}/audit/events");
        Assert.DoesNotContain(row.Ticket.ToString(), audit.GetRawText());
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task Historical_full_entitlement_filters_records_before_pagination_and_rejects_uncovered_details(bool bookingNow)
    {
        var commands = new ReadCounter();
        await using var app = await Phase13ApiFixture.CreateAsync(true, commands); var period = await SubscriptionTestData.ReplaceAsync(app);
        var end = app.Clock.UtcNow.AddDays(-1); var start = end.AddDays(-3);
        await app.WithDbAsync(async db =>
        {
            // Remove the fixture's former Premium period so only the explicit accepted interval proves history.
            var old = await db.DoctorSubscriptions.Where(s => s.DoctorId == app.DoctorId && s.Id != period).Select(s => s.Id).ToArrayAsync();
            db.RemoveRange(await db.SubscriptionFeatureSnapshots.Where(f => old.Contains(f.SubscriptionId)).ToArrayAsync());
            db.RemoveRange(await db.DoctorSubscriptions.Where(s => old.Contains(s.Id)).ToArrayAsync());
            var subscription = await db.DoctorSubscriptions.SingleAsync(s => s.Id == period);
            db.Entry(subscription).Property(s => s.StartsAtUtc).CurrentValue = start;
            db.Entry(subscription).Property(s => s.EndsAtUtc).CurrentValue = end;
            foreach (var a in await db.PracticeSubscriptionAllocations.Where(a => a.SubscriptionContractId == subscription.SubscriptionContractId).ToArrayAsync())
            {
                db.Entry(a).Property(a => a.AllocatedFromUtc).CurrentValue = start;
                if (a.ReleasedAtUtc is not null) db.Entry(a).Property(a => a.ReleasedAtUtc).CurrentValue = end;
            }
            subscription.Expire(app.Clock.UtcNow); await db.SaveChangesAsync();
        });
        var first = await HistoricalAsync(app, start.AddHours(1), 1); var second = await HistoricalAsync(app, end.AddHours(-2), 2);
        var tooEarly = await HistoricalAsync(app, start.AddHours(-2), 3); var tooLate = await HistoricalAsync(app, end.AddHours(2), 4);
        if (bookingNow) await SubscriptionTestData.BookingOnlyAsync(app, preserveHistory: true);
        await ArchiveAsync(app);
        using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception"); using var patient = await app.ClientAsync("patient");
        var url = $"/api/v1/doctors/me/practices/{app.PracticeId}/archives/tickets";
        commands.Reads = 0; var small = await GetAsync(doctor, url + "?pageSize=1"); var smallReads = commands.Reads;
        commands.Reads = 0; var all = await GetAsync(doctor, url + "?pageSize=10"); var allReads = commands.Reads;
        Assert.InRange(allReads, 1, 35); Assert.InRange(allReads - smallReads, -2, 2);
        Assert.Equal(new[] { first.Ticket, second.Ticket }.Order(), all.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("ticketId").GetGuid()).Order());
        Assert.NotNull(small.GetProperty("nextCursor").GetString());
        var next = await GetAsync(doctor, url + "?pageSize=1&cursor=" + Uri.EscapeDataString(small.GetProperty("nextCursor").GetString()!));
        Assert.False(next.GetProperty("hasMore").GetBoolean()); Assert.Single(next.GetProperty("items").EnumerateArray());
        await GetAsync(doctor, app.TicketUrl(first.Ticket));
        var assigned = await GetAsync(reception, $"/api/v1/reception/practices/{app.PracticeId}/archives/tickets"); Assert.Equal(2, assigned.GetProperty("items").GetArrayLength());
        foreach (var forbidden in new[] { tooEarly.Ticket, tooLate.Ticket })
        {
            using var denied = await doctor.GetAsync(app.TicketUrl(forbidden), TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            AssertNoProtectedPayload(await denied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), forbidden);
        }
        var own = await GetAsync(patient, "/api/v1/tickets/mine/archived"); Assert.Equal(4, own.GetProperty("items").GetArrayLength());
        var audit = await GetAsync(doctor, $"/api/v1/doctors/me/practices/{app.PracticeId}/audit/events");
        Assert.DoesNotContain(tooEarly.Ticket.ToString(), audit.GetRawText()); Assert.DoesNotContain(tooLate.Ticket.ToString(), audit.GetRawText());
        Guid uncommitted = default;
        await app.WithDbAsync(async db =>
        {
            // A selectable post-expiry Ticket must still be refused by the queue's deferred resource check.
            var at = app.Clock.UtcNow; uncommitted = Guid.NewGuid();
            db.Add(Ticket.CreateWalkIn(new(uncommitted, app.DoctorId, app.PracticeId, app.OtherPatientId, null, app.Today,
                900, TicketSource.WalkIn, app.SegmentId, "Synthetic", "Synthetic", 0, app.ConsultationTypeId,
                "NewConsultation", "Synthetic", "Synthetic", 300, "Africa/Cairo", at, at, CheckInMode.WalkIn, app.DoctorUserId, null)).Value);
            await db.SaveChangesAsync();
        });
        using var walkIn = await PostAsync(reception, $"/api/v1/practices/{app.PracticeId}/tickets/walk-in", new { patientId = app.PatientId, segmentId = app.SegmentId, visitTypeId = app.ConsultationTypeId, paidAmount = 300, paymentMethod = "Cash" });
        Assert.Equal(HttpStatusCode.Forbidden, walkIn.StatusCode);
        using var queue = await PostAsync(doctor, $"/api/v1/practices/{app.PracticeId}/queue/call-next", new { }); Assert.Equal(HttpStatusCode.Forbidden, queue.StatusCode);
        await app.WithDbAsync(async db => Assert.Equal(TicketStatus.Waiting, (await db.Tickets.SingleAsync(t => t.Id == uncommitted)).Status));
        await app.WithDbAsync(async db => { (await db.ApplicationUsers.SingleAsync(u => u.Id == app.DoctorUserId)).Deactivate(); await db.SaveChangesAsync(); });
        using var inactive = await doctor.GetAsync(url, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Forbidden, inactive.StatusCode);
    }

    [Fact]
    public async Task Legacy_snapshot_proves_old_owned_records_but_stops_at_paid_supersession()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true);
        await app.WithDbAsync(async db =>
        {
            db.RemoveRange(await db.PracticeSubscriptionAllocations.Where(a => db.DoctorPractices.Any(p => p.Id == a.DoctorPracticeId && p.DoctorId == app.DoctorId)).ToArrayAsync());
            var initial = await db.DoctorSubscriptions.Where(s => s.DoctorId == app.DoctorId).Select(s => s.Id).ToArrayAsync();
            db.RemoveRange(await db.SubscriptionFeatureSnapshots.Where(f => initial.Contains(f.SubscriptionId)).ToArrayAsync());
            db.RemoveRange(await db.DoctorSubscriptions.Where(s => initial.Contains(s.Id)).ToArrayAsync()); await db.SaveChangesAsync();
        });
        await using (var scope = app.Services.CreateAsyncScope())
            scope.ServiceProvider.GetRequiredService<IOptionsMonitor<Wasla.Application.Features.Subscriptions.SubscriptionCatalogOptions>>().CurrentValue.LegacyTransition.Enabled = true;
        using var admin = await app.ClientAsync("root");
        using var enrollment = await PostAsync(admin, "/api/v1/admin/subscriptions/legacy-transition/enroll", new { }); Assert.Equal(HttpStatusCode.OK, enrollment.StatusCode);
        var old = await HistoricalAsync(app, app.Clock.UtcNow.AddDays(-10), 1); string accepted = ""; Guid legacyId = default;
        await app.WithDbAsync(async db => { var legacy = await db.DoctorSubscriptions.SingleAsync(s => s.DoctorId == app.DoctorId); Assert.Equal(SubscriptionKind.LegacyTransition, legacy.SubscriptionKind); accepted = legacy.TermsSnapshotJson; legacyId = legacy.Id; });
        app.Clock.UtcNow = app.Clock.UtcNow.AddDays(1); await SubscriptionTestData.BookingOnlyAsync(app, preserveHistory: true);
        var newer = await HistoricalAsync(app, app.Clock.UtcNow.AddHours(1), 2); app.Clock.UtcNow = app.Clock.UtcNow.AddDays(2);
        await ArchiveAsync(app); using var doctor = await app.ClientAsync("doctor");
        var page = await GetAsync(doctor, $"/api/v1/doctors/me/practices/{app.PracticeId}/archives/tickets");
        Assert.Equal(old.Ticket, Assert.Single(page.GetProperty("items").EnumerateArray()).GetProperty("ticketId").GetGuid());
        await GetAsync(doctor, app.TicketUrl(old.Ticket));
        using var denied = await doctor.GetAsync(app.TicketUrl(newer.Ticket), TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await app.WithDbAsync(async db => Assert.Equal(accepted, (await db.DoctorSubscriptions.SingleAsync(s => s.Id == legacyId)).TermsSnapshotJson));
    }

    [Fact]
    public async Task Guardian_archive_selector_and_resource_reads_keep_existing_relationship_checks_without_a_subscription_prerequisite()
    {
        await using var family = await Phase16FamilyFixture.CreateAsync(true); var app = family.App;
        await SubscriptionTestData.BookingOnlyAsync(app);
        var row = await HistoricalAsync(app, app.Clock.UtcNow.AddDays(-2), 1, family.ChildId); await ArchiveAsync(app);
        var url = "/api/v1/reservations/mine/archived?patientId=" + family.ChildId;
        var page = await GetAsync(family.Father, url); Assert.Single(page.GetProperty("items").EnumerateArray());
        await GetAsync(family.Father, "/api/v1/reservations/mine/" + row.Reservation);
        await app.WithDbAsync(async db =>
        {
            var household = await db.Families.Include(f => f.Members).SingleAsync(f => f.Id == family.FamilyId);
            db.Entry(household.Members.Single(m => m.PatientId == family.ChildId)).Property("IsActive").CurrentValue = false;
            await db.SaveChangesAsync();
        });
        using var denied = await family.Father.GetAsync(url, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.DoesNotContain(row.Reservation.ToString(), await denied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private sealed class ReadCounter : DbCommandInterceptor
    {
        public int Reads;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Reads); return ValueTask.FromResult(result); }
    }
    private static void AssertNoProtectedPayload(string body, Guid suppliedId)
    {
        using var problem = System.Text.Json.JsonDocument.Parse(body);
        // Wasla's existing ProblemDetails instance echoes the caller-supplied request path.
        // It must not disclose protected DTOs or any resource identifier in the error payload.
        foreach (var property in problem.RootElement.EnumerateObject().Where(p => p.Name != "instance"))
            Assert.DoesNotContain(suppliedId.ToString(), property.Value.GetRawText());
        Assert.False(problem.RootElement.TryGetProperty("ticketId", out _)); Assert.False(problem.RootElement.TryGetProperty("patientId", out _));
        Assert.DoesNotContain("patientName", body); Assert.DoesNotContain("SqlException", body);
    }
}

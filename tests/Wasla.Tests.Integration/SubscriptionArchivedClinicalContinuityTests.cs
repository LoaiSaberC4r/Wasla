using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Subscriptions;
using Wasla.Application.Persistence;
using Wasla.Domain.Archives;
using Wasla.Domain.Subscriptions;
using Wasla.Domain.Tickets;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class SubscriptionArchivedClinicalContinuityTests
{
    [Fact]
    public async Task Archived_linked_ticket_and_encounter_keep_the_original_reservation_commitment()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true);
        var visit = await CompletedVisitAsync(app, linkedReservation: true);
        Assert.True(visit.ReservationCreatedAtUtc < visit.TicketCreatedAtUtc);
        await AssertCommitmentAsync(app, new(SubscriptionResourceKind.Encounter, visit.EncounterId), visit.ReservationCreatedAtUtc!.Value);

        await ArchiveAsync(app, visit);
        await ExpireAsync(app, visit, visit.TicketCreatedAtUtc.AddMinutes(1));

        foreach (var resource in new[]
        {
            new SubscriptionResource(SubscriptionResourceKind.Reservation, visit.ReservationId!.Value),
            new SubscriptionResource(SubscriptionResourceKind.Ticket, visit.TicketId),
            new SubscriptionResource(SubscriptionResourceKind.Encounter, visit.EncounterId)
        })
            await AssertCommitmentAsync(app, resource, visit.ReservationCreatedAtUtc.Value);

        // Historical read coverage uses the actual Ticket's creation time, independently of operational continuity.
        await using var scope = app.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ISubscriptionDataStore>();
        var ticketRead = await store.ReadCommitmentAsync(new(SubscriptionResourceKind.Ticket, visit.TicketId), TestContext.Current.CancellationToken);
        Assert.NotNull(ticketRead); Assert.Equal(app.DoctorId, ticketRead.DoctorId); Assert.Equal(visit.TicketCreatedAtUtc, ticketRead.CreatedOnUtc);
        Assert.Equal(app.PracticeId, ticketRead.PracticeId); Assert.Equal(visit.TicketId, ticketRead.TicketId); Assert.Equal(visit.ReservationId, ticketRead.ReservationId);
        var reservationRead = await store.ReadCommitmentAsync(new(SubscriptionResourceKind.Reservation, visit.ReservationId.Value), TestContext.Current.CancellationToken);
        Assert.NotNull(reservationRead); Assert.Equal(app.DoctorId, reservationRead.DoctorId); Assert.Equal(visit.ReservationCreatedAtUtc.Value, reservationRead.CreatedOnUtc);
        Assert.Equal(app.PracticeId, reservationRead.PracticeId); Assert.Equal(visit.ReservationId, reservationRead.ReservationId);

    }

    [Fact]
    public async Task Archived_direct_ticket_and_encounter_keep_the_original_ticket_commitment()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true);
        var visit = await CompletedVisitAsync(app, linkedReservation: false);
        Assert.Null(visit.ReservationId);
        await AssertCommitmentAsync(app, new(SubscriptionResourceKind.Encounter, visit.EncounterId), visit.TicketCreatedAtUtc);

        await ArchiveAsync(app, visit);
        await ExpireAsync(app, visit, visit.TicketCreatedAtUtc.AddMinutes(1));
        await AssertCommitmentAsync(app, new(SubscriptionResourceKind.Ticket, visit.TicketId), visit.TicketCreatedAtUtc);
        await AssertCommitmentAsync(app, new(SubscriptionResourceKind.Encounter, visit.EncounterId), visit.TicketCreatedAtUtc);

        await using var scope = app.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ISubscriptionDataStore>();
        foreach (var kind in new[] { SubscriptionResourceKind.Reservation, SubscriptionResourceKind.Ticket, SubscriptionResourceKind.Encounter })
        {
            var missing = new SubscriptionResource(kind, Guid.NewGuid());
            Assert.Null(await store.CommitmentAsync(missing, TestContext.Current.CancellationToken));
            Assert.Null(await store.ReadCommitmentAsync(missing, TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task Eligible_archived_clinical_amendment_succeeds_without_restoring_operational_or_cross_doctor_access()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true);
        var visit = await CompletedVisitAsync(app, linkedReservation: true);
        await ArchiveAsync(app, visit);
        await ExpireAsync(app, visit, visit.TicketCreatedAtUtc.AddMinutes(1));
        using var doctor = await app.ClientAsync("doctor");
        var encounter = await GetAsync(doctor, app.EncounterUrl(visit.EncounterId));
        var amendment = Amendment(encounter.GetProperty("rowVersion").GetString()!);
        using var changed = await PostAsync(doctor, app.EncounterUrl(visit.EncounterId) + "/amendments", amendment);
        Assert.True(changed.StatusCode == HttpStatusCode.Created, await changed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Reviewed clinical correction", (await JsonAsync(changed)).GetProperty("clinicalNotes").GetString());

        using var otherDoctor = await app.ClientAsync("other-doctor");
        using var foreign = await PostAsync(otherDoctor, app.EncounterUrl(visit.EncounterId) + "/amendments", amendment);
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        using var wrongPractice = await PostAsync(doctor,
            $"/api/v1/doctors/me/practices/{app.OtherPracticeId}/encounters/{visit.EncounterId}/amendments", amendment);
        Assert.Equal(HttpStatusCode.Forbidden, wrongPractice.StatusCode);

        var ticket = await GetAsync(doctor, app.TicketUrl(visit.TicketId));
        Assert.True(ticket.GetProperty("isArchived").GetBoolean());
        var capabilities = ticket.GetProperty("operationalCapabilities");
        foreach (var action in new[] { "canCancel", "canRestore", "canQueueMutate", "canMutateOperationalRecord" })
            Assert.False(capabilities.GetProperty(action).GetBoolean());
        using var cancel = await PostAsync(doctor, app.TicketUrl(visit.TicketId) + "/cancel",
            new { reason = "Must remain archived", rowVersion = ticket.GetProperty("rowVersion").GetString() });
        // A fresh mutation cannot load an archived Ticket from the operational write repository.
        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);

        using var reception = await app.ClientAsync("reception");
        using var newTicket = await PostAsync(reception, $"/api/v1/practices/{app.PracticeId}/tickets/walk-in",
            new { patientId = app.PatientId, segmentId = app.SegmentId, visitTypeId = app.ConsultationTypeId, paidAmount = 300, paymentMethod = "Cash" });
        Assert.Equal(HttpStatusCode.Forbidden, newTicket.StatusCode);
        using var patient = await app.ClientAsync("patient");
        using var newBooking = await PostAsync(patient, "/api/v1/reservations", Booking(app));
        Assert.Equal(HttpStatusCode.Forbidden, newBooking.StatusCode);

        await app.WithDbAsync(async db =>
        {
            var original = await db.MedicalEncounters.Include(e => e.Amendments).SingleAsync(e => e.Id == visit.EncounterId);
            Assert.Single(original.Amendments);
            Assert.Equal("Reviewed clinical correction", original.ClinicalNotes);
            Assert.False(await db.Tickets.AnyAsync(t => t.Id == visit.TicketId));
            Assert.Equal(TicketStatus.Completed, (await db.Set<ArchivedTicket>().SingleAsync(t => t.Id == visit.TicketId)).Status);
            Assert.Empty(await db.Reservations.ToArrayAsync());
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Archived_commitment_at_or_after_expiration_still_denies_clinical_amendments(int secondsAfterExpiration)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true);
        var visit = await CompletedVisitAsync(app, linkedReservation: false);
        await ArchiveAsync(app, visit);
        await ExpireAsync(app, visit, visit.TicketCreatedAtUtc.AddSeconds(-secondsAfterExpiration));
        await AssertCommitmentAsync(app, new(SubscriptionResourceKind.Encounter, visit.EncounterId), visit.TicketCreatedAtUtc);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var denied = await scope.ServiceProvider.GetRequiredService<ISubscriptionOperationalContinuityGuard>()
                .EvaluateAsync(app.DoctorId, "MEDICAL_ENCOUNTER", new(SubscriptionResourceKind.Encounter, visit.EncounterId), TestContext.Current.CancellationToken);
            Assert.True(denied.IsFailure);
            Assert.Contains(denied.Errors, error => error.Code == "Subscription.Expired");
        }
        using var doctor = await app.ClientAsync("doctor");
        using var changed = await PostAsync(doctor, app.EncounterUrl(visit.EncounterId) + "/amendments", Amendment(visit.EncounterRowVersion));
        Assert.Equal(HttpStatusCode.Forbidden, changed.StatusCode);
        await app.WithDbAsync(async db =>
        {
            var original = await db.MedicalEncounters.Include(e => e.Amendments).SingleAsync(e => e.Id == visit.EncounterId);
            Assert.Empty(original.Amendments);
            Assert.Equal("Clinical documentation", original.ClinicalNotes);
        });
    }

    private sealed record Visit(Guid TicketId, Guid? ReservationId, Guid EncounterId, Guid SubscriptionId,
        DateTime TicketCreatedAtUtc, DateTime? ReservationCreatedAtUtc, string EncounterRowVersion);

    private static object Booking(Phase13ApiFixture app) => new
    {
        patientId = app.PatientId, doctorPracticeId = app.PracticeId, businessDate = app.Today, slotStartTime = "18:00",
        segmentId = app.SegmentId, visitTypeId = app.ConsultationTypeId
    };

    private static object Amendment(string rowVersion) => new
    {
        reason = "Reviewed archived clinical documentation", encounterRowVersion = rowVersion,
        changes = new[] { new { target = "ClinicalNotes", changeType = "Updated", clinicalNotes = "Reviewed clinical correction" } }
    };

    private static async Task<Visit> CompletedVisitAsync(Phase13ApiFixture app, bool linkedReservation)
    {
        using var doctor = await app.ClientAsync("doctor");
        using var reception = await app.ClientAsync("reception");
        System.Text.Json.JsonElement ticket;
        if (linkedReservation)
        {
            using var patient = await app.ClientAsync("patient");
            using var booked = await PostAsync(patient, "/api/v1/reservations", Booking(app));
            Assert.True(booked.StatusCode == HttpStatusCode.Created, await booked.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var reservationId = (await JsonAsync(booked)).GetProperty("reservationId").GetGuid();
            app.Clock.UtcNow = app.Clock.UtcNow.Date.AddHours(14).AddMinutes(40);
            using var checkedIn = await PostAsync(reception, $"/api/v1/practices/{app.PracticeId}/reservations/{reservationId}/check-in",
                new { paidAmount = 300, paymentMethod = "Cash" });
            Assert.True(checkedIn.StatusCode == HttpStatusCode.Created, await checkedIn.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            ticket = await JsonAsync(checkedIn);
        }
        else ticket = await app.WalkInAsync(reception);
        var completed = await app.CompleteAsync(doctor, await app.StartAsync(doctor, ticket));
        var ticketId = completed.Ticket.GetProperty("ticketId").GetGuid();
        var encounterId = completed.Ticket.GetProperty("medicalEncounterId").GetGuid();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
        var original = await db.Tickets.SingleAsync(t => t.Id == ticketId);
        var reservationCreated = original.ReservationId is { } id
            ? await db.Reservations.Where(r => r.Id == id).Select(r => (DateTime?)r.CreatedOnUtc).SingleAsync() : null;
        var subscriptionId = await db.DoctorSubscriptions.Where(s => s.DoctorId == app.DoctorId && s.Status == SubscriptionStatus.Active).Select(s => s.Id).SingleAsync();
        Assert.Contains(await db.SubscriptionFeatureSnapshots.Where(f => f.SubscriptionId == subscriptionId).ToArrayAsync(),
            f => f.FeatureCodeSnapshot == "MEDICAL_ENCOUNTER" && f.AccessLevel == FeatureAccessLevel.Full);
        return new(ticketId, original.ReservationId, encounterId, subscriptionId, original.CreatedOnUtc,
            reservationCreated, completed.Encounter.GetProperty("rowVersion").GetString()!);
    }

    private static async Task ArchiveAsync(Phase13ApiFixture app, Visit visit)
    {
        app.Clock.UtcNow = app.Clock.UtcNow.AddDays(1);
        await using var scope = app.Services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ArchiveOptions>>().Value;
        options.Enabled = true;
        options.ReadModelsVerified = true;
        await scope.ServiceProvider.GetRequiredService<ArchiveOperationalDays>().ProcessAsync(TestContext.Current.CancellationToken);
        var engine = scope.ServiceProvider.GetRequiredService<ArchiveEngine>();
        Assert.True(await engine.ReadyAsync(TestContext.Current.CancellationToken));
        for (var i = 0; i < 3; i++) Assert.NotNull(await engine.ProcessAsync(TestContext.Current.CancellationToken));
        await app.WithDbAsync(async db =>
        {
            Assert.False(await db.Tickets.AnyAsync(t => t.Id == visit.TicketId));
            Assert.True(await db.Set<ArchivedTicket>().AnyAsync(t => t.Id == visit.TicketId));
            Assert.True(await db.MedicalEncounters.AnyAsync(e => e.Id == visit.EncounterId));
            if (visit.ReservationId is { } id)
            {
                Assert.False(await db.Reservations.AnyAsync(r => r.Id == id));
                Assert.True(await db.Set<ArchivedReservation>().AnyAsync(r => r.Id == id));
            }
        });
    }

    private static async Task ExpireAsync(Phase13ApiFixture app, Visit visit, DateTime end)
    {
        await app.WithDbAsync(async db =>
        {
            var subscription = await db.DoctorSubscriptions.SingleAsync(s => s.Id == visit.SubscriptionId);
            db.Entry(subscription).Property(s => s.EndsAtUtc).CurrentValue = end;
            await db.SaveChangesAsync();
        });
        await SubscriptionApiTests.Process(app, visit.SubscriptionId);
        await app.WithDbAsync(async db => Assert.Equal(SubscriptionStatus.Expired,
            (await db.DoctorSubscriptions.SingleAsync(s => s.Id == visit.SubscriptionId)).Status));
    }

    private static async Task AssertCommitmentAsync(Phase13ApiFixture app, SubscriptionResource resource, DateTime createdAtUtc)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var commitment = await scope.ServiceProvider.GetRequiredService<ISubscriptionDataStore>()
            .CommitmentAsync(resource, TestContext.Current.CancellationToken);
        Assert.NotNull(commitment); Assert.Equal(app.DoctorId, commitment.DoctorId); Assert.Equal(createdAtUtc, commitment.CreatedOnUtc);
        Assert.Equal(app.PracticeId, commitment.PracticeId); if (resource.Kind != SubscriptionResourceKind.Reservation) Assert.NotNull(commitment.TicketId);
    }
}

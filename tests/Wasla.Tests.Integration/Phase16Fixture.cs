using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Clinical;
using Wasla.Domain.Clinical;
using Wasla.Domain.Practices;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;
using Wasla.Domain.Reservations;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase15DiagnosticApiTests;

namespace Wasla.Tests.Integration;

internal static class Phase16Fixture
{
    internal static string TreatingUrl(Phase13ApiFixture app, Guid ticket) => $"/api/v1/doctors/me/practices/{app.OtherPracticeId}/tickets/{ticket}/medical-record";
    internal static async Task<(Guid Ticket, Guid Encounter, Guid Doctor, Guid User)> TreatingAsync(Phase13ApiFixture app, bool start = true, Guid? patient = null, TicketSource source = TicketSource.WalkIn)
    {
        var result = (Ticket: Guid.Empty, Encounter: Guid.Empty, Doctor: Guid.Empty, User: Guid.Empty);
        await app.WithDbAsync(async db =>
        {
            var ct = TestContext.Current.CancellationToken;
            var d = await db.Doctors.SingleAsync(d => d.Id != app.DoctorId, ct);
            var segment = await db.DoctorPracticeSegments.FirstOrDefaultAsync(s => s.DoctorPracticeId == app.OtherPracticeId, ct) ?? DoctorPracticeSegment.CreateDefault(Guid.NewGuid(), app.OtherPracticeId, d.ApplicationUserId).Value;
            var type = await db.DoctorPracticeVisitTypes.FirstOrDefaultAsync(s => s.DoctorPracticeId == app.OtherPracticeId, ct) ?? DoctorPracticeVisitType.CreateDefault(Guid.NewGuid(), app.OtherPracticeId, DoctorPracticeVisitTypeCode.NewConsultation, d.ApplicationUserId).Value;
            if (db.Entry(segment).State == EntityState.Detached) db.DoctorPracticeSegments.Add(segment);
            if (db.Entry(type).State == EntityState.Detached) db.DoctorPracticeVisitTypes.Add(type);
            Guid? reservationId = null;
            if (source == TicketSource.Reservation)
            {
                var reservation = Reservation.Create(new ReservationCreationSnapshot(Guid.NewGuid(), Guid.NewGuid().ToString("N"), d.Id, app.OtherPracticeId, patient ?? app.PatientId, segment.Id, type.Id, ReservationBookingSource.Reception, d.ApplicationUserId, null, app.Clock.UtcNow, DateTime.SpecifyKind(app.Clock.UtcNow.AddHours(3), DateTimeKind.Unspecified), app.Today, "Africa/Cairo", 20, "Synthetic", "Synthetic", 0, "NewConsultation", "Synthetic", "Synthetic", 300, null, app.Clock.UtcNow)).Value;
                reservation.ConvertToTicket(d.ApplicationUserId, app.Clock.UtcNow); db.Reservations.Add(reservation); reservationId = reservation.Id;
            }
            var number = (await db.Tickets.Where(t => t.DoctorPracticeId == app.OtherPracticeId && t.BusinessDate == app.Today).MaxAsync(t => (int?)t.TicketNumber, ct) ?? 0) + 1;
            var snapshot = new TicketCreationSnapshot(Guid.NewGuid(), d.Id, app.OtherPracticeId, patient ?? app.PatientId, reservationId,
                app.Today, number, source, segment.Id, "عادي", "Normal", 0, type.Id, "NewConsultation", "كشف", "Visit", 300,
                "Africa/Cairo", app.Clock.UtcNow, app.Clock.UtcNow, source == TicketSource.WalkIn ? CheckInMode.WalkIn : CheckInMode.Normal, d.ApplicationUserId, null);
            var ticket = (source == TicketSource.WalkIn ? Ticket.CreateWalkIn(snapshot) : Ticket.CreateFromReservation(snapshot)).Value;
            if (start) { ticket.Call(d.ApplicationUserId, app.Clock.UtcNow); ticket.StartVisit(d.ApplicationUserId, app.Clock.UtcNow); }
            db.Tickets.Add(ticket);
            var e = start ? MedicalEncounter.Start(ticket, d.ApplicationUserId, app.Clock.UtcNow).Value : null;
            if (e is not null) db.MedicalEncounters.Add(e);
            await db.SaveChangesAsync(ct); result = (ticket.Id, e?.Id ?? Guid.Empty, d.Id, d.ApplicationUserId);
        });
        return result;
    }
    internal static void EnableSharing(Phase13ApiFixture app)
    {
        var options = app.Services.GetRequiredService<IOptions<MedicalRecordOptions>>().Value;
        options.CrossDoctorSharingEnabled = true; options.OpaqueEntryKey = "explicit-phase16-test-opaque-entry-secret-32-characters";
    }
    internal static async Task<JsonElement> HideAsync(Phase13ApiFixture app, HttpClient doctor, Guid id)
    {
        var e = await GetAsync(doctor, app.EncounterUrl(id));
        return await OkAsync(await doctor.PatchAsJsonAsync(app.EncounterUrl(id) + "/visibility", new { visibility = "Hidden", rowVersion = e.GetProperty("rowVersion").GetString() }, TestContext.Current.CancellationToken));
    }
}

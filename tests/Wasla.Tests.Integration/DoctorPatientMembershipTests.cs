using System.Net;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Common;
using Wasla.Domain.Patients;
using Wasla.Domain.Practices;
using Wasla.Domain.Tickets;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class DoctorPatientMembershipTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Doctor_wide_identity_search_and_statistics_deduplicate_practices_and_retain_terminal_admissions(bool sql)
    {
        await using var f = await Phase13ApiFixture.CreateAsync(sql); using var doctor = await f.ClientAsync("doctor");
        Guid closedPractice = default; Guid minorId = Guid.NewGuid();
        await f.WithDbAsync(async db =>
        {
            var patient = await db.Patients.SingleAsync(p => p.Id == f.PatientId);
            patient.UpdateProfile("الاسم الحالي", "Current Patient", "+20 100-123-4567", "current@example.test");
            var minor = Patient.Create(minorId, "طفل", "Child", f.Today.AddYears(-9), Gender.Female, null, null, null, null, null, f.Today).Value;
            db.Patients.Add(minor);
            for (var i = 0; i < 3; i++)
            {
                var practiceId = i == 0 ? f.PracticeId : Guid.NewGuid();
                var segment = i == 0 ? f.SegmentId : Guid.NewGuid(); var visitType = i == 0 ? f.ConsultationTypeId : Guid.NewGuid();
                if (i > 0)
                {
                    var practice = DoctorPractice.Create(practiceId, f.DoctorId, "عيادة " + i, "Practice " + i, 1, 1, 1, "Address", 30, 31, f.DoctorUserId).Value;
                    if (i == 1) practice.Activate(true, true, true, f.DoctorUserId); else closedPractice = practiceId;
                    db.DoctorPractices.Add(practice);
                    db.DoctorPracticeConfigurations.Add(DoctorPracticeConfiguration.CreateDefault(Guid.NewGuid(), practiceId, f.DoctorUserId).Value);
                    db.DoctorPracticeSegments.Add(DoctorPracticeSegment.CreateDefault(segment, practiceId, f.DoctorUserId).Value);
                    db.DoctorPracticeVisitTypes.Add(DoctorPracticeVisitType.CreateDefault(visitType, practiceId, DoctorPracticeVisitTypeCode.NewConsultation, f.DoctorUserId).Value);
                }
                var time = f.Clock.UtcNow.AddMinutes(-3 + i);
                var ticket = Ticket.CreateWalkIn(new(Guid.NewGuid(), f.DoctorId, practiceId, f.PatientId, null, f.Today, 1,
                    TicketSource.WalkIn, segment, "عادي", "Normal", 0, visitType, "NewConsultation", "كشف", "Consultation", 300,
                    "Africa/Cairo", time, time, CheckInMode.WalkIn, f.DoctorUserId, null)).Value;
                if (i == 0) ticket.Cancel(f.DoctorUserId, "Patient left after check-in", time);
                if (i == 1) { ticket.Call(f.DoctorUserId, time); ticket.ConfirmNoResponse(f.DoctorUserId, 1, time); }
                db.Tickets.Add(ticket);
                if (i == 2)
                {
                    var childTicket = Ticket.CreateWalkIn(new(Guid.NewGuid(), f.DoctorId, practiceId, minorId, null, f.Today, 2,
                        TicketSource.WalkIn, segment, "عادي", "Normal", 0, visitType, "NewConsultation", "كشف", "Consultation", 300,
                        "Africa/Cairo", time, time, CheckInMode.WalkIn, f.DoctorUserId, null)).Value;
                    db.Tickets.Add(childTicket);
                }
            }
            await db.SaveChangesAsync();
        });
        var directory = await GetAsync(doctor, "/api/v1/doctors/me/patients"); Assert.Equal(2, directory.GetProperty("totalCount").GetInt32());
        var own = directory.GetProperty("items").EnumerateArray().Single(p => p.GetProperty("patientId").GetGuid() == f.PatientId);
        Assert.Equal(3, own.GetProperty("attendanceCount").GetInt32()); Assert.Equal(3, own.GetProperty("practiceCount").GetInt32());
        Assert.Equal(0, own.GetProperty("completedEncounterCount").GetInt32()); Assert.Equal("الاسم الحالي", own.GetProperty("nameAr").GetString());
        Assert.Equal(closedPractice, own.GetProperty("lastPractice").GetProperty("id").GetGuid()); Assert.False(own.GetProperty("historicalOnly").GetBoolean());
        Assert.Equal(1, directory.GetProperty("summary").GetProperty("returningPatients").GetInt32());
        foreach (var term in new[] { "الاسم", "Current", "+201001234567", "CURRENT@EXAMPLE.TEST" })
        {
            var found = await GetAsync(doctor, "/api/v1/doctors/me/patients?search=" + Uri.EscapeDataString(term));
            Assert.Equal(1, found.GetProperty("totalCount").GetInt32()); Assert.Equal(f.PatientId, found.GetProperty("items")[0].GetProperty("patientId").GetGuid());
        }
        var historical = await GetAsync(doctor, "/api/v1/doctors/me/patients?practiceScope=historicalOnly");
        Assert.Equal(minorId, historical.GetProperty("items")[0].GetProperty("patientId").GetGuid());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, historical.GetProperty("items")[0].GetProperty("phoneNumber").ValueKind);
        var narrowed = await GetAsync(doctor, $"/api/v1/doctors/me/patients?practiceId={closedPractice}");
        Assert.All(narrowed.GetProperty("items").EnumerateArray(), p => Assert.Equal(1, p.GetProperty("attendanceCount").GetInt32()));
        var returning = await GetAsync(doctor, "/api/v1/doctors/me/patients?minAttendanceCount=2"); Assert.Equal(1, returning.GetProperty("totalCount").GetInt32());
        using var foreign = await doctor.GetAsync($"/api/v1/doctors/me/patients?practiceId={f.OtherPracticeId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }
    [Fact]
    public async Task Reservation_without_check_in_is_not_membership_but_successful_check_in_is()
    {
        await using var f = await Phase13ApiFixture.CreateAsync(); using var doctor = await f.ClientAsync("doctor"); using var patient = await f.ClientAsync("patient"); using var reception = await f.ClientAsync("reception");
        using var booking = await PostAsync(patient, "/api/v1/reservations", new { patientId = f.PatientId, doctorPracticeId = f.PracticeId,
            businessDate = f.Today, slotStartTime = "18:00", segmentId = f.SegmentId, visitTypeId = f.ConsultationTypeId });
        Assert.Equal(HttpStatusCode.Created, booking.StatusCode); var id = (await JsonAsync(booking)).GetProperty("reservationId").GetGuid();
        Assert.Equal(0, (await GetAsync(doctor, "/api/v1/doctors/me/patients")).GetProperty("totalCount").GetInt32());
        f.Clock.UtcNow = f.Clock.UtcNow.Date.AddHours(14).AddMinutes(40);
        using var admitted = await PostAsync(reception, $"/api/v1/practices/{f.PracticeId}/reservations/{id}/check-in", new { paidAmount = 300, paymentMethod = "Cash" });
        Assert.Equal(HttpStatusCode.Created, admitted.StatusCode);
        Assert.Equal(1, (await GetAsync(doctor, "/api/v1/doctors/me/patients")).GetProperty("totalCount").GetInt32());
    }
}

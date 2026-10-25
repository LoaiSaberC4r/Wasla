using BuildingBlock.Application.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Domain.Patients;
using Wasla.Domain.Reservations;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Notifications;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Tests.Integration;

internal static class Phase17NotificationFixture
{
    internal static async Task<Guid> ReservationAsync(Phase13ApiFixture app, DateTime? starts = null, Guid? patientId = null, bool trusted = false)
    {
        var id = Guid.NewGuid();
        await app.WithDbAsync(async db =>
        {
            var patient = patientId ?? app.PatientId;
            var link = await db.PatientAccountLinks.SingleOrDefaultAsync(l => l.PatientId == patient);
            var actor = link?.ApplicationUserId ?? app.DoctorUserId;
            if (trusted && link is not null) await TrustEmailAsync(db, link.ApplicationUserId, app.Clock.UtcNow);
            var start = starts ?? app.Clock.UtcNow.AddHours(4);
            var local = TimeZoneInfo.ConvertTimeFromUtc(start, TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"));
            var r = Reservation.Create(new(id, "WSL-R-" + id.ToString("N")[..12], app.DoctorId, app.PracticeId, patient,
                app.SegmentId, app.ConsultationTypeId, ReservationBookingSource.Patient, actor, null,
                start, DateTime.SpecifyKind(local, DateTimeKind.Unspecified), DateOnly.FromDateTime(local), "Africa/Cairo", 20,
                "عادي", "Normal", 1, "NewConsultation", "كشف جديد", "New consultation", 300, "PRIVATE_BOOKING_NOTE", app.Clock.UtcNow)).Value;
            db.Reservations.Add(r); await db.SaveChangesAsync();
        });
        return id;
    }
    internal static async Task TrustEmailAsync(WaslaDbContext db, Guid userId, DateTime now)
    {
        var user = await db.ApplicationUsers.SingleAsync(u => u.Id == userId);
        var proof = new PatientOnboardingChallenge(Guid.NewGuid(), PatientOnboardingPurpose.EmailChange, user.Email, userId, null, "test-only-hash", now, now.AddMinutes(10), 5);
        proof.Verify("test-token", PatientOnboardingAction.EmailChange, now, now.AddMinutes(5)); proof.Consume(now, "test-only-fingerprint");
        db.PatientOnboardingChallenges.Add(proof);
    }
    internal static async Task<int> DispatchAsync(Phase13ApiFixture app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<NotificationDispatchProcessor>().ProcessBatchAsync(TestContext.Current.CancellationToken);
    }
    internal static async Task<int> SendEmailsAsync(Phase13ApiFixture app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var sender = new Sender();
        var processor = new Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email.EmailOutboxProcessor(
            scope.ServiceProvider.GetRequiredService<WaslaDbContext>(), sender, app.Clock,
            Microsoft.Extensions.Options.Options.Create(new Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options.EmailOutboxOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email.EmailOutboxProcessor>.Instance,
            scope.ServiceProvider.GetRequiredService<NotificationEmailGuard>());
        await processor.ProcessBatchAsync(TestContext.Current.CancellationToken);
        return sender.Messages.Count;
    }
    internal sealed class Sender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = [];
        public Exception? Failure { get; set; }
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        { if (Failure is { } failure) throw failure; Messages.Add(message); return Task.CompletedTask; }
    }
}

using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using BuildingBlock.Application.Email;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Practices;
using Wasla.Domain.Security;
using Wasla.Domain.Subscriptions;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class CommercialV12RemindersAndTransferTests
{
    private static readonly string[] PaidStages = ["D7", "D3", "D1", "D0"];
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Reminder_dispatch_rechecks_paid_successor_even_if_an_old_message_remains_pending(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); using var doctor = await app.ClientAsync("doctor"); using var admin = await app.ClientAsync("root");
        var period = await CommercialV12ApiTests.SelectAndActivateAsync(doctor, admin, "STARTER", "dispatch-proof");
        var id = period.GetProperty("id").GetGuid(); var details = await GetAsync(admin, "/api/v1/admin/subscriptions/" + id);
        using var paid = await PostAsync(admin, $"/api/v1/admin/subscriptions/{id}/renew", new { rowVersion = details.GetProperty("rowVersion").GetString(), amountReceived = 499, paymentMethod = "Cash" });
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
        // Simulate an old notice restored/retried after the payment transaction already suppressed its queue.
        var key = $"subscription-paid-reminder:{id}:0:D0";
        await new EmailOutbox(db, app.Clock).QueueAsync(new Wasla.Application.Email.QueueEmailMessage(key, "doctor@example.test", "Expiry", "Expiry"), TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var message = await db.EmailOutboxMessages.AsNoTracking().SingleAsync(m => m.IdempotencyKey == key, TestContext.Current.CancellationToken);
        var sender = new CapturingSender();
        var processor = new EmailOutboxProcessor(db, sender, app.Clock, Options.Create(new EmailOutboxOptions { BatchSize = 1000 }),
            NullLogger<EmailOutboxProcessor>.Instance, subscriptionGuard: new SubscriptionEmailGuard(db));
        await processor.ProcessBatchAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(sender.Messages, m => m.Subject == "Expiry");
        var saved = await db.EmailOutboxMessages.AsNoTracking().SingleAsync(m => m.Id == message.Id, TestContext.Current.CancellationToken);
        Assert.Equal(EmailOutboxStatus.Suppressed, saved.Status); Assert.Equal("Subscription.PaidSuccessorConfirmed", saved.LastError);
    }
    private sealed class CapturingSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = [];
        public Task SendAsync(EmailMessage message, CancellationToken ct = default) { Messages.Add(message); return Task.CompletedTask; }
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Paid_reminders_are_per_period_and_confirmed_successor_suppresses_only_its_own_old_expiry(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); using var doctor = await app.ClientAsync("doctor"); using var admin = await app.ClientAsync("root");
        var one = await CommercialV12ApiTests.SelectAndActivateAsync(doctor, admin, "STARTER", "reminder-one");
        var two = await CommercialV12ApiTests.SelectAndActivateAsync(doctor, admin, "STARTER", "reminder-two");
        var oneId = one.GetProperty("id").GetGuid(); var twoId = two.GetProperty("id").GetGuid(); var end = one.GetProperty("endsAtUtc").GetDateTime();
        await app.WithDbAsync(async db =>
        {
            var schedules = await db.SubscriptionReminderSchedules.Where(r => r.SubscriptionId == oneId).OrderBy(r => r.DueAtUtc).ToArrayAsync();
            Assert.Equal(PaidStages, schedules.Select(r => r.ReminderCode));
            Assert.Equal(new[] { end.AddDays(-7), end.AddDays(-3), end.AddDays(-1), end }, schedules.Select(r => r.DueAtUtc));
        });
        app.Clock.UtcNow = end.AddDays(-7); await SubscriptionApiTests.Process(app, oneId); await SubscriptionApiTests.Process(app, twoId);
        var details = await GetAsync(admin, "/api/v1/admin/subscriptions/" + oneId);
        using var renewed = await PostAsync(admin, $"/api/v1/admin/subscriptions/{oneId}/renew", new { rowVersion = details.GetProperty("rowVersion").GetString(), amountReceived = 499, paymentMethod = "Cash" });
        Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);
        app.Clock.UtcNow = end; await SubscriptionApiTests.Process(app, oneId); await SubscriptionApiTests.Process(app, twoId); await SubscriptionApiTests.Process(app, twoId);
        await app.WithDbAsync(async db =>
        {
            Assert.False(await db.EmailOutboxMessages.AnyAsync(e => e.IdempotencyKey == $"subscription-paid-reminder:{oneId}:0:D0"));
            Assert.Single(await db.EmailOutboxMessages.Where(e => e.IdempotencyKey == $"subscription-paid-reminder:{twoId}:0:D0").ToArrayAsync());
            var securedNotice = Assert.Single(await db.EmailOutboxMessages.Where(e => e.IdempotencyKey == $"subscription-paid-reminder:{oneId}:0:D7").ToArrayAsync());
            Assert.Equal(Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email.EmailOutboxStatus.Suppressed, securedNotice.Status);
        });
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task Transfer_retains_selected_identity_only_without_deactivating_shared_accounts_or_other_practice_assignments(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); using var doctor = await app.ClientAsync("doctor"); using var admin = await app.ClientAsync("root");
        Guid sourceContract = default, ahmed = default, sara = default, otherPractice = default;
        await app.WithDbAsync(async db => sourceContract = await db.DoctorSubscriptions.Where(p => p.DoctorId == app.DoctorId).Select(p => p.SubscriptionContractId).SingleAsync());
        using var created = await PostAsync(doctor, "/api/v1/doctors/me/practices", new { nameAr = "عيادة أخرى", governorateId = 1, cityId = 1, areaId = 1, detailedAddress = "Address", latitude = 30, longitude = 31, subscriptionContractId = sourceContract });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); otherPractice = (await JsonAsync(created)).GetProperty("id").GetGuid();
        await app.WithDbAsync(async db =>
        {
            ahmed = await db.Receptions.Where(r => r.OwnerDoctorId == app.DoctorId).Select(r => r.Id).SingleAsync();
            var user = ApplicationUser.Create(Guid.NewGuid(), "sara", "sara@example.test", null, "hash", UserType.Reception, false, app.Clock.UtcNow).Value;
            var reception = Reception.Create(Guid.NewGuid(), user.Id, app.DoctorId, "سارة", null, app.DoctorUserId).Value; sara = reception.Id;
            db.Add(user); db.Add(reception);
            db.Add(ReceptionPracticeAssignment.Create(Guid.NewGuid(), sara, app.PracticeId, app.DoctorUserId).Value);
            db.Add(ReceptionPracticeAssignment.Create(Guid.NewGuid(), sara, otherPractice, app.DoctorUserId).Value); await db.SaveChangesAsync();
            db.Add(ReceptionPracticeAssignment.Create(Guid.NewGuid(), ahmed, otherPractice, app.DoctorUserId).Value); await db.SaveChangesAsync();
        });
        var target = await CommercialV12ApiTests.SelectAndActivateAsync(doctor, admin, "STARTER", "staff-target");
        var targetContract = target.GetProperty("subscriptionContractId").GetGuid();
        var allocation = await GetAsync(doctor, $"/api/v1/doctors/me/practices/{app.PracticeId}/subscription-allocation");
        using var moved = await PostAsync(doctor, $"/api/v1/doctors/me/practices/{app.PracticeId}/subscription-allocation/move", new
        { targetSubscriptionContractId = targetContract, continuedReceptionIds = new[] { ahmed }, rowVersion = allocation.GetProperty("allocation").GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var sourceDetails = (await GetAsync(doctor, "/api/v1/doctors/me/subscriptions/" + sourceContract))[0];
        var targetDetails = (await GetAsync(doctor, "/api/v1/doctors/me/subscriptions/" + targetContract))[0];
        Assert.Equal(2, sourceDetails.GetProperty("usage").GetProperty("receptionCount").GetInt32());
        Assert.Equal(1, targetDetails.GetProperty("usage").GetProperty("receptionCount").GetInt32());
        await app.WithDbAsync(async db =>
        {
            Assert.False((await db.ReceptionPracticeAssignments.SingleAsync(a => a.ReceptionId == sara && a.DoctorPracticeId == app.PracticeId)).IsActive);
            Assert.True((await db.ReceptionPracticeAssignments.SingleAsync(a => a.ReceptionId == sara && a.DoctorPracticeId == otherPractice)).IsActive);
            Assert.True((await db.ReceptionPracticeAssignments.SingleAsync(a => a.ReceptionId == ahmed && a.DoctorPracticeId == app.PracticeId)).IsActive);
            Assert.True((await db.ApplicationUsers.SingleAsync(u => u.UserName == "sara")).IsActive);
            Assert.Single(await db.PracticeSubscriptionAllocations.Where(a => a.DoctorPracticeId == app.PracticeId && a.ReleasedAtUtc == null && a.SubscriptionContractId == targetContract).ToArrayAsync());
            Assert.Equal(SubscriptionStatus.Active, (await db.DoctorSubscriptions.SingleAsync(p => p.SubscriptionContractId == sourceContract)).Status);
        });
    }
}

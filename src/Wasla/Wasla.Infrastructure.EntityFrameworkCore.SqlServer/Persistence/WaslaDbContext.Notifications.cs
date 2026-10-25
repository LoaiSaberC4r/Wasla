using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Notifications;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

public sealed partial class WaslaDbContext
{
    public DbSet<NotificationEvent> NotificationEvents => Set<NotificationEvent>();
    public DbSet<NotificationIntent> NotificationIntents => Set<NotificationIntent>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<PlatformNotificationSettings> PlatformNotificationSettings => Set<PlatformNotificationSettings>();
    public DbSet<PlatformNotificationSettingsAudit> PlatformNotificationSettingsAudits => Set<PlatformNotificationSettingsAudit>();
    public DbSet<NotificationAudit> NotificationAudits => Set<NotificationAudit>();

    private static void ConfigureNotifications(ModelBuilder model)
    {
        var contacts = model.Entity<NotificationVerifiedContact>();
        contacts.ToTable("NotificationVerifiedContacts"); contacts.HasKey(x => x.Id);
        contacts.Property(x => x.AddressFingerprint).HasMaxLength(64);
        contacts.HasIndex(x => new { x.ApplicationUserId, x.PatientId, x.AddressFingerprint }).IsUnique();
        contacts.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        contacts.HasOne<Domain.Patients.Patient>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        var events = model.Entity<NotificationEvent>();
        events.ToTable("NotificationEvents"); events.HasKey(x => x.Id);
        events.HasIndex(x => x.IdempotencyKey).IsUnique();
        events.HasIndex(x => new { x.SourceType, x.SourceId, x.SourceRevisionKey });
        events.Property(x => x.IdempotencyKey).HasMaxLength(300);
        events.Property(x => x.EventType).HasMaxLength(80); events.Property(x => x.SourceType).HasMaxLength(30);
        events.Property(x => x.SourceEventKey).HasMaxLength(100); events.Property(x => x.SourceRevisionKey).HasMaxLength(100);
        events.Property(x => x.RowVersion).IsRowVersion();
        var intents = model.Entity<NotificationIntent>();
        intents.ToTable("NotificationIntents"); intents.HasKey(x => x.Id);
        intents.HasIndex(x => x.NotificationEventId).IsUnique();
        intents.HasIndex(x => new { x.Status, x.DueAtUtc }); intents.HasIndex(x => new { x.Status, x.NextAttemptOnUtc });
        intents.Property(x => x.SourceRevisionKey).HasMaxLength(100); intents.Property(x => x.SkipReasonCode).HasMaxLength(80);
        intents.Property(x => x.RowVersion).IsRowVersion();
        intents.HasOne<NotificationEvent>().WithMany().HasForeignKey(x => x.NotificationEventId).OnDelete(DeleteBehavior.Restrict);
        var deliveries = model.Entity<NotificationDelivery>();
        deliveries.ToTable("NotificationDeliveries"); deliveries.HasKey(x => x.Id);
        deliveries.Property(x => x.LogicalDeliveryKey).HasMaxLength(450); deliveries.HasIndex(x => x.LogicalDeliveryKey).IsUnique();
        deliveries.Property(x => x.RecipientLogicalKey).HasMaxLength(100); deliveries.Property(x => x.TemplateCode).HasMaxLength(80);
        deliveries.Property(x => x.SafeFailureCode).HasMaxLength(80); deliveries.Property(x => x.RowVersion).IsRowVersion();
        deliveries.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        deliveries.HasOne<NotificationEvent>().WithMany().HasForeignKey(x => x.NotificationEventId).OnDelete(DeleteBehavior.Restrict);
        deliveries.HasOne<NotificationIntent>().WithMany().HasForeignKey(x => x.NotificationIntentId).OnDelete(DeleteBehavior.Restrict);
        var inbox = model.Entity<UserNotification>();
        inbox.ToTable("UserNotifications"); inbox.HasKey(x => x.Id);
        inbox.HasIndex(x => new { x.NotificationDeliveryId, x.ApplicationUserId }).IsUnique();
        inbox.HasIndex(x => new { x.ApplicationUserId, x.CreatedAtUtc, x.Id }).IsDescending(false, true, true);
        inbox.HasIndex(x => new { x.ApplicationUserId, x.ReadAtUtc, x.CreatedAtUtc });
        inbox.Property(x => x.EventType).HasMaxLength(80); inbox.Property(x => x.TemplateCode).HasMaxLength(80);
        inbox.Property(x => x.SafePreviewCode).HasMaxLength(80); inbox.Property(x => x.TargetResourceType).HasMaxLength(30);
        inbox.Property(x => x.TargetResourceKey).HasMaxLength(50); inbox.Property(x => x.RowVersion).IsRowVersion();
        inbox.HasOne<NotificationDelivery>().WithMany().HasForeignKey(x => x.NotificationDeliveryId).OnDelete(DeleteBehavior.Restrict);
        inbox.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        var preferences = model.Entity<NotificationPreference>();
        preferences.ToTable("NotificationPreferences"); preferences.HasKey(x => x.Id);
        preferences.HasIndex(x => new { x.ApplicationUserId, x.Category, x.Channel }).IsUnique();
        preferences.Property(x => x.PreferredLanguage).HasMaxLength(2); preferences.Property(x => x.RowVersion).IsRowVersion();
        preferences.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        var settings = model.Entity<PlatformNotificationSettings>();
        settings.ToTable("PlatformNotificationSettings", t =>
        {
            t.HasCheckConstraint("CK_NotificationSettings_Singleton", "[Id] = '17000000-0000-0000-0000-000000000001'");
            t.HasCheckConstraint("CK_NotificationSettings_Lead", "[ReminderLeadMinutes] BETWEEN 1 AND 43200");
        });
        settings.HasKey(x => x.Id); settings.Property(x => x.RowVersion).IsRowVersion();
        settings.HasData(new PlatformNotificationSettings());
        var audits = model.Entity<PlatformNotificationSettingsAudit>();
        audits.ToTable("PlatformNotificationSettingsAudits"); audits.HasKey(x => x.Id);
        audits.HasIndex(x => x.NewVersion).IsUnique(); audits.Property(x => x.Reason).HasMaxLength(500);
        var actions = model.Entity<NotificationAudit>();
        actions.ToTable("NotificationAudits"); actions.HasKey(x => x.Id); actions.HasIndex(x => x.OccurredAtUtc);
        actions.Property(x => x.Action).HasMaxLength(80); actions.Property(x => x.OutcomeCode).HasMaxLength(80);
        actions.Property(x => x.Reason).HasMaxLength(500);
        model.Entity<Email.EmailOutboxMessage>().HasOne<NotificationDelivery>().WithMany()
            .HasForeignKey(x => x.NotificationDeliveryId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Email.EmailOutboxMessage>().HasIndex(x => x.NotificationDeliveryId).IsUnique().HasFilter("[NotificationDeliveryId] IS NOT NULL");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Wasla.Domain.Archives;
using Wasla.Domain.Tickets;
using Wasla.Domain.Reservations;
using Wasla.Domain.Practices;
using Wasla.Domain.Doctors;
using Wasla.Domain.Patients;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

public sealed partial class WaslaDbContext
{
    public DbSet<TicketRegistry> TicketRegistry => Set<TicketRegistry>();
    public DbSet<ReservationRegistry> ReservationRegistry => Set<ReservationRegistry>();
    public DbSet<PracticeOperationalDay> PracticeOperationalDays => Set<PracticeOperationalDay>();
    public DbSet<ArchiveControlState> ArchiveControlStates => Set<ArchiveControlState>();
    public DbSet<ArchiveRun> ArchiveRuns => Set<ArchiveRun>();
    public DbSet<ArchiveIntegrityIssue> ArchiveIntegrityIssues => Set<ArchiveIntegrityIssue>();
    public DbSet<ArchiveAuditEvent> ArchiveAuditEvents => Set<ArchiveAuditEvent>();
    public DbSet<ArchiveControlRequest> ArchiveControlRequests => Set<ArchiveControlRequest>();
    public DbSet<ArchiveProgress> ArchiveProgress => Set<ArchiveProgress>();

    private bool HasNewArchiveIdentities => ChangeTracker.Entries().Any(x => x.State == EntityState.Added && x.Entity is Ticket or Reservation);
    private void PrepareArchiveIdentities()
    {
        foreach (var reservation in ChangeTracker.Entries<Reservation>().Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray())
            if (!ReservationRegistry.Local.Any(x => x.Id == reservation.Id)) ReservationRegistry.Add(Domain.Archives.ReservationRegistry.Register(reservation));
        foreach (var ticket in ChangeTracker.Entries<Ticket>().Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray())
            if (!TicketRegistry.Local.Any(x => x.Id == ticket.Id)) TicketRegistry.Add(Domain.Archives.TicketRegistry.Register(ticket));
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted &&
                (entry.Entity is ArchiveAuditEvent or ArchiveControlRequest or ArchiveIntegrityIssue || entry.Metadata.ClrType.Name.StartsWith("Archived", StringComparison.Ordinal)))
                throw new InvalidOperationException("Archive.ImmutableHistory");
            if (entry.State == EntityState.Deleted && entry.Entity is Ticket or Reservation or TicketHistory or TicketCallAttempt or ReservationHistory or Domain.Archives.TicketRegistry or Domain.Archives.ReservationRegistry or PracticeOperationalDay)
                throw new InvalidOperationException("Archive.NoDisposal");
            if (entry.State == EntityState.Modified && entry.Entity is PracticeOperationalDay &&
                entry.Property(nameof(PracticeOperationalDay.ClosedAtUtc)).OriginalValue != null)
                throw new InvalidOperationException("Archive.ClosedDayImmutable");
            if (entry.State == EntityState.Modified && entry.Entity is Domain.Archives.TicketRegistry or Domain.Archives.ReservationRegistry)
            {
                if (entry.Property("IsArchived").OriginalValue is true || entry.Properties.Any(x => x.IsModified && x.Metadata.Name is not ("IsArchived" or "ArchivedAtUtc" or "ArchiveSequence" or "Provenance" or "RowVersion")))
                    throw new InvalidOperationException("Archive.ImmutableIdentity");
            }
        }
    }

    private bool ArchiveSqlite => Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
    private void ConfigureArchives(ModelBuilder model)
    {
        var ticket = model.Entity<TicketRegistry>();
        ticket.ToTable("TicketRegistry", t => t.HasCheckConstraint("CK_TicketRegistry_Location", "([IsArchived] = 0 AND [ArchivedAtUtc] IS NULL) OR ([IsArchived] = 1 AND [ArchivedAtUtc] IS NOT NULL)"));
        ticket.HasKey(x => x.Id); ticket.Property(x => x.Id).ValueGeneratedNever();
        ticket.HasAlternateKey(x => new { x.DoctorPracticeId, x.Id });
        ticket.HasIndex(x => new { x.DoctorPracticeId, x.BusinessDate, x.TicketNumber }).IsUnique();
        ticket.HasIndex(x => x.ReservationId).IsUnique().HasFilter("[ReservationId] IS NOT NULL");
        ticket.Property(x => x.RowVersion).IsRowVersion(); ticket.Property(x => x.Provenance).HasMaxLength(40);
        ticket.HasOne<ReservationRegistry>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        ticket.HasOne<Doctor>().WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        ticket.HasOne<DoctorPractice>().WithMany().HasForeignKey(x => x.DoctorPracticeId).OnDelete(DeleteBehavior.Restrict);
        ticket.HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        var reservation = model.Entity<ReservationRegistry>();
        reservation.ToTable("ReservationRegistry", t => t.HasCheckConstraint("CK_ReservationRegistry_Location", "([IsArchived] = 0 AND [ArchivedAtUtc] IS NULL) OR ([IsArchived] = 1 AND [ArchivedAtUtc] IS NOT NULL)"));
        reservation.HasKey(x => x.Id); reservation.Property(x => x.Id).ValueGeneratedNever();
        reservation.HasAlternateKey(x => new { x.DoctorPracticeId, x.Id });
        reservation.Property(x => x.ReservationReference).HasMaxLength(32);
        reservation.Property(x => x.Provenance).HasMaxLength(40);
        reservation.HasIndex(x => x.ReservationReference).IsUnique(); reservation.Property(x => x.RowVersion).IsRowVersion();
        reservation.HasOne<Doctor>().WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        reservation.HasOne<DoctorPractice>().WithMany().HasForeignKey(x => x.DoctorPracticeId).OnDelete(DeleteBehavior.Restrict);
        reservation.HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        var day = model.Entity<PracticeOperationalDay>(); day.HasKey(x => x.Id);
        day.HasIndex(x => new { x.DoctorPracticeId, x.BusinessDate }).IsUnique(); day.Property(x => x.RowVersion).IsRowVersion();
        day.Property(x => x.TimeZoneId).HasMaxLength(100); day.Property(x => x.EvidenceHash).HasMaxLength(64);
        day.HasOne<DoctorPractice>().WithMany().HasForeignKey(x => x.DoctorPracticeId).OnDelete(DeleteBehavior.Restrict);
        var control = model.Entity<ArchiveControlState>(); control.HasKey(x => x.Id); control.Property(x => x.Id).ValueGeneratedNever();
        control.Property(x => x.RowVersion).IsRowVersion();
        control.Property(x => x.EventKeySecret).HasMaxLength(32).IsRequired();
        var run = model.Entity<ArchiveRun>(); run.HasKey(x => x.Id);
        run.Property(x => x.Mode).HasMaxLength(40); run.Property(x => x.Outcome).HasMaxLength(40); run.Property(x => x.CorrelationId).HasMaxLength(64);
        run.HasIndex(x => new { x.StartedAtUtc, x.Id });
        var exception = model.Entity<ArchiveIntegrityIssue>(); exception.HasKey(x => x.Id);
        exception.Property(x => x.EntityType).HasMaxLength(40); exception.Property(x => x.FailureCode).HasMaxLength(80);
        exception.Property(x => x.StateFingerprint).HasMaxLength(64);
        exception.HasIndex(x => new { x.EntityId, x.EntityType, x.FailureCode, x.StateFingerprint }).IsUnique();
        exception.HasIndex(x => new { x.OccurredAtUtc, x.Id });
        exception.HasOne<ArchiveRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
        var audit = model.Entity<ArchiveAuditEvent>(); audit.HasKey(x => x.Id);
        audit.Property(x => x.Action).HasMaxLength(100); audit.Property(x => x.EntityType).HasMaxLength(40);
        audit.Property(x => x.Outcome).HasMaxLength(40); audit.Property(x => x.ReasonHash).HasMaxLength(64);
        audit.Property(x => x.FailureCode).HasMaxLength(80); audit.Property(x => x.CorrelationId).HasMaxLength(64);
        audit.HasIndex(x => new { x.DoctorPracticeId, x.OccurredAtUtc, x.Id });
        audit.HasIndex(x => new { x.ActorId, x.OccurredAtUtc, x.Id });
        var request = model.Entity<ArchiveControlRequest>(); request.HasKey(x => x.Id);
        request.Property(x => x.IdempotencyKey).HasMaxLength(200); request.Property(x => x.Fingerprint).HasMaxLength(64);
        request.Property(x => x.ResultRowVersion).HasMaxLength(40);
        request.Property(x => x.Operation).HasMaxLength(20);
        request.HasIndex(x => new { x.ActorId, x.Operation, x.IdempotencyKey }).IsUnique();
        day.Property(x => x.EvidenceVerified).HasDefaultValue(true);
        model.Entity<ArchiveProgress>().HasKey(x => x.Id);
        model.Entity<ArchiveProgress>().Property(x => x.Id).HasMaxLength(80);
        model.Entity<ArchiveProgress>().Property(x => x.Mode).HasMaxLength(40);
        ConfigureArchiveCopy<Ticket, ArchivedTicket>(model, "Tickets");
        ConfigureArchiveCopy<Reservation, ArchivedReservation>(model, "Reservations");
        ConfigureArchiveCopy<TicketHistory, ArchivedTicketHistory>(model, "TicketHistories");
        ConfigureArchiveCopy<TicketCallAttempt, ArchivedTicketCallAttempt>(model, "TicketCallAttempts");
        ConfigureArchiveCopy<ReservationHistory, ArchivedReservationHistory>(model, "ReservationHistories");
    }

    private void ConfigureArchiveCopy<TSource, TArchive>(ModelBuilder model, string table) where TSource : class where TArchive : class
    {
        var source = model.Entity<TSource>().Metadata;
        var target = model.Entity<TArchive>();
        target.ToTable(ArchiveSqlite ? "Archived" + table : table, ArchiveSqlite ? null : "archive", tableBuilder =>
        {
            foreach (var constraint in source.GetCheckConstraints()) tableBuilder.HasCheckConstraint("CK_Archived" + table + "_" + constraint.Name, constraint.Sql);
        });
        target.HasKey("Id"); target.Property("Id").ValueGeneratedNever();
        foreach (var property in source.GetProperties().ToArray())
        {
            var field = target.Property(property.ClrType, property.Name).IsRequired(!property.IsNullable);
            if (property.GetColumnType() is { } columnType) field.HasColumnType(property.Name == "RowVersion" ? "binary(8)" : columnType);
            if (property.GetMaxLength() is { } length) field.HasMaxLength(length);
            if (property.GetPrecision() is { } precision) field.HasPrecision(precision, property.GetScale() ?? 0);
            if (property.Name == "RowVersion") field.ValueGeneratedNever().IsConcurrencyToken(false);
        }
        foreach (var key in source.GetForeignKeys().ToArray())
        {
            var principal = key.PrincipalEntityType.ClrType;
            if (principal == typeof(Ticket)) principal = typeof(ArchivedTicket);
            if (principal == typeof(Reservation)) principal = typeof(ReservationRegistry);
            target.HasOne(principal).WithMany().HasForeignKey(key.Properties.Select(x => x.Name).ToArray())
                .HasPrincipalKey(key.PrincipalKey.Properties.Select(x => x.Name).ToArray()).OnDelete(DeleteBehavior.Restrict);
        }
        if (typeof(TSource) == typeof(Ticket)) target.HasOne<TicketRegistry>().WithMany().HasForeignKey("Id").OnDelete(DeleteBehavior.Restrict);
        if (typeof(TSource) == typeof(Reservation)) target.HasOne<ReservationRegistry>().WithMany().HasForeignKey("Id").OnDelete(DeleteBehavior.Restrict);
        if (typeof(TSource) == typeof(ReservationHistory))
        {
            // Archive history must reference its archive physical parent, not a live parent.
            var registryKey = target.Metadata.GetForeignKeys().Single(x => x.Properties.Count == 1 && x.Properties[0].Name == "ReservationId");
            target.Metadata.RemoveForeignKey(registryKey);
            target.HasOne<ArchivedReservation>().WithMany().HasForeignKey("ReservationId").OnDelete(DeleteBehavior.Restrict);
        }
        foreach (var index in source.GetIndexes().Where(x => x.GetFilter() == null).ToArray())
            target.HasIndex(index.Properties.Select(x => x.Name).ToArray()).IsUnique(index.IsUnique)
                .HasDatabaseName("IX_Archived" + table + "_" + string.Join('_', index.Properties.Select(x => x.Name)));
        if (typeof(TSource) == typeof(Ticket) || typeof(TSource) == typeof(Reservation))
        {
            target.HasIndex("DoctorPracticeId", "BusinessDate", "Id");
            target.HasIndex("PatientId", "BusinessDate", "Id");
        }
    }

    // Read-only composed SQL unions preserve the established domain projection contracts.
    // Archived rows are never returned from write repositories or operational DbSets.
    public IQueryable<Ticket> AllTickets => Historical<Ticket>();
    public IQueryable<Reservation> AllReservations => Historical<Reservation>();
    public IQueryable<TicketHistory> AllTicketHistories => Historical<TicketHistory>();
    public IQueryable<TicketCallAttempt> AllTicketCallAttempts => Historical<TicketCallAttempt>();
    public IQueryable<ReservationHistory> AllReservationHistories => Historical<ReservationHistory>();

    private IQueryable<T> Historical<T>() where T : class
    {
        var entity = Model.FindEntityType(typeof(T))!;
        var table = entity.GetTableName()!;
        var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
        var columns = string.Join(",", entity.GetProperties().Select(x => "[" + x.GetColumnName(store) + "]"));
        var active = ArchiveSqlite ? "[" + table + "]" : "[dbo].[" + table + "] WITH (HOLDLOCK)";
        var archive = ArchiveSqlite ? "[Archived" + table + "]" : "[archive].[" + table + "] WITH (HOLDLOCK)";
        var sql = $"SELECT {columns} FROM {active} UNION ALL SELECT {columns} FROM {archive}";
        return Set<T>().FromSqlRaw(sql).AsNoTracking();
    }
}

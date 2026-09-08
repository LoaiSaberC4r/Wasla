using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email;
using Wasla.Domain.Doctors;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;
using Wasla.Domain.ReferenceData;
using BuildingBlock.Infrastructure.Extensions;
using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

public sealed class WaslaDbContext(DbContextOptions<WaslaDbContext> options)
    : DbContext(options)
{
    public DbSet<EmailOutboxMessage> EmailOutboxMessages => Set<EmailOutboxMessage>();
    public DbSet<ApplicationUser> ApplicationUsers => Set<ApplicationUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<SuperAdmin> SuperAdmins => Set<SuperAdmin>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<DoctorStatusHistory> DoctorStatusHistories => Set<DoctorStatusHistory>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<PasswordResetChallenge> PasswordResetChallenges => Set<PasswordResetChallenge>();
    public DbSet<MedicalSpecialization> MedicalSpecializations => Set<MedicalSpecialization>();
    public DbSet<DoctorSpecialization> DoctorSpecializations => Set<DoctorSpecialization>();
    public DbSet<DoctorSpecializationRequest> DoctorSpecializationRequests => Set<DoctorSpecializationRequest>();
    public DbSet<DoctorSpecializationRequestRevision> DoctorSpecializationRequestRevisions => Set<DoctorSpecializationRequestRevision>();
    public DbSet<DoctorSpecializationRequestItem> DoctorSpecializationRequestItems => Set<DoctorSpecializationRequestItem>();
    public DbSet<DoctorSpecializationRequestHistory> DoctorSpecializationRequestHistories => Set<DoctorSpecializationRequestHistory>();
    public DbSet<Governorate> Governorates => Set<Governorate>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<DoctorPracticeLocation> DoctorPracticeLocations => Set<DoctorPracticeLocation>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareSqliteRowVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        PrepareSqliteRowVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyWriteConfigurations(typeof(WaslaDbContext).Assembly);
        modelBuilder.ApplySoftDeleteQueryFilter();

        if (string.Equals(
                Database.ProviderName,
                "Microsoft.EntityFrameworkCore.Sqlite",
                StringComparison.Ordinal))
        {
            ConfigureSqliteConcurrencyFallback(modelBuilder);
        }
    }

    private static void ConfigureSqliteConcurrencyFallback(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var rowVersion = entityType.FindProperty(nameof(EmailOutboxMessage.RowVersion));
            if (rowVersion is null)
            {
                continue;
            }

            rowVersion.ValueGenerated = ValueGenerated.Never;
            rowVersion.SetBeforeSaveBehavior(PropertySaveBehavior.Save);
            rowVersion.SetAfterSaveBehavior(PropertySaveBehavior.Save);
        }
    }

    private void PrepareSqliteRowVersions()
    {
        if (!string.Equals(
                Database.ProviderName,
                "Microsoft.EntityFrameworkCore.Sqlite",
                StringComparison.Ordinal))
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            var rowVersion = entry.Metadata.FindProperty(nameof(EmailOutboxMessage.RowVersion));
            if (rowVersion is not null)
            {
                entry.Property(nameof(EmailOutboxMessage.RowVersion)).CurrentValue =
                    Guid.NewGuid().ToByteArray()[..8];
            }
        }
    }
}

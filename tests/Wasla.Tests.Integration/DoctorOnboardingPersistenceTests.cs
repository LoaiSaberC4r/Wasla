using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.ReferenceData;
using Wasla.Domain.Security;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Tests.Integration;

public sealed class DoctorOnboardingPersistenceTests
{
    private static readonly DateTime Now = new(2026, 9, 8, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid RootUserId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task MedicalSpecialization_UniqueNamesIncludeSoftDeletedRows()
    {
        await using var database = await TestDatabase.CreateAsync();
        AddRoot(database.Context);
        var first = MedicalSpecialization.Create(
            Guid.NewGuid(), "أمراض القلب", "Cardiology", null, null, 10, RootUserId).Value;
        database.Context.MedicalSpecializations.Add(first);
        await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        first.Deactivate(RootUserId);
        first.SoftDelete(RootUserId);
        await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        database.Context.MedicalSpecializations.Add(MedicalSpecialization.Create(
            Guid.NewGuid(), "أمراض القلب", null, null, null, 20, RootUserId).Value);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            database.Context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MedicalSpecialization_UniqueEnglishNameIncludesSoftDeletedRows()
    {
        await using var database = await TestDatabase.CreateAsync();
        AddRoot(database.Context);
        var first = MedicalSpecialization.Create(
            Guid.NewGuid(), "تخصص أول", "Shared Name", null, null, 10, RootUserId).Value;
        database.Context.MedicalSpecializations.Add(first);
        await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        first.Deactivate(RootUserId);
        first.SoftDelete(RootUserId);
        await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        database.Context.MedicalSpecializations.Add(MedicalSpecialization.Create(
            Guid.NewGuid(), "تخصص ثان", "Shared Name", null, null, 20, RootUserId).Value);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            database.Context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MedicalSpecialization_AllowsMultipleNullEnglishNames()
    {
        await using var database = await TestDatabase.CreateAsync();
        AddRoot(database.Context);
        database.Context.MedicalSpecializations.AddRange(
            MedicalSpecialization.Create(Guid.NewGuid(), "تخصص أول", null, null, null, 10, RootUserId).Value,
            MedicalSpecialization.Create(Guid.NewGuid(), "تخصص ثان", null, null, null, 20, RootUserId).Value);

        await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, await database.Context.MedicalSpecializations.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EffectiveSpecializations_DatabaseRejectsTwoPrimaryRows()
    {
        await using var database = await TestDatabase.CreateAsync();
        var doctor = AddDoctorGraph(database.Context);
        var first = AddSpecialization(database.Context, "القلب", "Cardiology");
        var second = AddSpecialization(database.Context, "الباطنة", "Internal Medicine");
        await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        database.Context.DoctorSpecializations.AddRange(
            new DoctorSpecialization(Guid.NewGuid(), doctor.Id, first.Id, true, RootUserId, Now),
            new DoctorSpecialization(Guid.NewGuid(), doctor.Id, second.Id, true, RootUserId, Now));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            database.Context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RequestRevisionItems_DatabaseRejectsTwoPrimaryRows()
    {
        await using var database = await TestDatabase.CreateAsync();
        var doctor = AddDoctorGraph(database.Context);
        var first = AddSpecialization(database.Context, "القلب", "Cardiology");
        var second = AddSpecialization(database.Context, "الباطنة", "Internal Medicine");
        var request = DoctorSpecializationRequest.Create(
            Guid.NewGuid(), doctor.Id, DoctorSpecializationRequestType.Initial, doctor.ApplicationUserId, Now).Value;
        var revision = new DoctorSpecializationRequestRevision(
            Guid.NewGuid(), request.Id, 1, doctor.ApplicationUserId, Now);
        database.Context.DoctorSpecializationRequests.Add(request);
        database.Context.DoctorSpecializationRequestRevisions.Add(revision);
        database.Context.DoctorSpecializationRequestItems.AddRange(
            new DoctorSpecializationRequestItem(Guid.NewGuid(), revision.Id, first.Id, true),
            new DoctorSpecializationRequestItem(Guid.NewGuid(), revision.Id, second.Id, true));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            database.Context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DoctorPracticeLocation_DatabaseAllowsOnlyOneRowPerDoctor()
    {
        await using var database = await TestDatabase.CreateAsync();
        var doctor = AddDoctorGraph(database.Context);
        database.Context.Governorates.Add(Governorate.Create(1, "القاهرة", "Cairo", 1).Value);
        database.Context.Cities.Add(City.Create(1001, 1, "قسم التبين", "Al Tibbin", 1).Value);
        database.Context.Areas.Add(Area.Create(10010001, 1001, "التبين البحرية", "Area", 1).Value);
        database.Context.DoctorPracticeLocations.AddRange(
            DoctorPracticeLocation.Create(
                Guid.NewGuid(), doctor.Id, 1, 1001, 10010001, "العنوان الأول", 30m, 31m).Value,
            DoctorPracticeLocation.Create(
                Guid.NewGuid(), doctor.Id, 1, 1001, 10010001, "العنوان الثاني", 30m, 31m).Value);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            database.Context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FrozenSeeders_AreExactAndIdempotentWithoutOverwritingLifecycleChanges()
    {
        await using var database = await TestDatabase.CreateAsync();
        AddRoot(database.Context);
        await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await InvokeSeederAsync(database.Context, "MedicalSpecializationSeeder");
        await InvokeSeederAsync(database.Context, "EgyptLocationSeedCoordinator");

        Assert.Equal(47, await database.Context.MedicalSpecializations.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(27, await database.Context.Governorates.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(351, await database.Context.Cities.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(5716, await database.Context.Areas.CountAsync(TestContext.Current.CancellationToken));
        Assert.True(await database.Context.MedicalSpecializations.AllAsync(
            item => item.IsActive && !item.IsDeleted && item.DescriptionAr == null && item.DescriptionEn == null,
            TestContext.Current.CancellationToken));

        var seeded = await database.Context.MedicalSpecializations.SingleAsync(
            item => item.Id == Guid.Parse("61bc8e48-ddee-587c-a1f7-206a619842ec"),
            TestContext.Current.CancellationToken);
        seeded.Update("الباطنة المعدلة", "Modified Internal Medicine", null, null, 999, RootUserId);
        seeded.Deactivate(RootUserId);
        seeded.SoftDelete(RootUserId);
        await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        database.Context.ChangeTracker.Clear();

        await InvokeSeederAsync(database.Context, "MedicalSpecializationSeeder");
        await InvokeSeederAsync(database.Context, "EgyptLocationSeedCoordinator");

        Assert.Equal(47, await database.Context.MedicalSpecializations.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(5716, await database.Context.Areas.CountAsync(TestContext.Current.CancellationToken));
        var preserved = await database.Context.MedicalSpecializations.IgnoreQueryFilters().SingleAsync(
            item => item.Id == seeded.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal("الباطنة المعدلة", preserved.NameAr);
        Assert.Equal(999, preserved.SortOrder);
        Assert.False(preserved.IsActive);
        Assert.True(preserved.IsDeleted);
    }

    [Fact]
    public async Task MedicalSpecializationSeeder_FailsFastOnSemanticNameConflict()
    {
        await using var database = await TestDatabase.CreateAsync();
        AddRoot(database.Context);
        database.Context.MedicalSpecializations.Add(MedicalSpecialization.Create(
            Guid.NewGuid(), "الباطنة العامة", "Different English Name", null, null, 1, RootUserId).Value);
        await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            InvokeSeederAsync(database.Context, "MedicalSpecializationSeeder"));

        Assert.Equal(1, await database.Context.MedicalSpecializations.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EgyptSeeder_RollsBackEarlierStagesWhenCityIdentityConflicts()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Context.Governorates.Add(Governorate.Create(999, "محافظة اختبار", "Test Governorate", 1).Value);
        database.Context.Cities.Add(City.Create(1001, 999, "مدينة متعارضة", "Conflicting City", 1).Value);
        await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        database.Context.ChangeTracker.Clear();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            InvokeSeederAsync(database.Context, "EgyptLocationSeedCoordinator"));
        database.Context.ChangeTracker.Clear();

        Assert.Equal(1, await database.Context.Governorates.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await database.Context.Cities.CountAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await database.Context.Areas.ToListAsync(TestContext.Current.CancellationToken));
    }

    private static async Task InvokeSeederAsync(WaslaDbContext dbContext, string typeName)
    {
        var type = typeof(WaslaDbContext).Assembly.GetType(
            $"Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.{typeName}",
            throwOnError: true)!;
        var seeder = Activator.CreateInstance(
            type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            [dbContext],
            null)!;
        await (Task)type.GetMethod("SeedAsync")!.Invoke(
            seeder,
            [TestContext.Current.CancellationToken])!;
    }

    private static ApplicationUser AddRoot(WaslaDbContext context)
    {
        var user = ApplicationUser.Create(
            RootUserId,
            "root-seed",
            "root-seed@example.test",
            null,
            "hash",
            UserType.SuperAdmin,
            false,
            Now).Value;
        context.ApplicationUsers.Add(user);
        return user;
    }

    private static Doctor AddDoctorGraph(WaslaDbContext context)
    {
        AddRoot(context);
        var doctorUser = ApplicationUser.Create(
            Guid.NewGuid(), "doctor", "doctor@example.test", null, "hash", UserType.Doctor, false, Now).Value;
        var doctor = Doctor.Create(
            Guid.NewGuid(), doctorUser.Id, "طبيب", "Doctor", new DateOnly(1990, 1, 1), Gender.Male,
            null, "front", "back", "syndicate", null, new DateOnly(2026, 9, 8)).Value;
        context.ApplicationUsers.Add(doctorUser);
        context.Doctors.Add(doctor);
        return doctor;
    }

    private static MedicalSpecialization AddSpecialization(WaslaDbContext context, string nameAr, string nameEn)
    {
        var item = MedicalSpecialization.Create(Guid.NewGuid(), nameAr, nameEn, null, null, 1, RootUserId).Value;
        context.MedicalSpecializations.Add(item);
        return item;
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private TestDatabase(SqliteConnection connection, WaslaDbContext context)
            => (Connection, Context) = (connection, context);

        public SqliteConnection Connection { get; }
        public WaslaDbContext Context { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var context = new WaslaDbContext(new DbContextOptionsBuilder<WaslaDbContext>()
                .UseSqlite(connection)
                .Options);
            await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            return new TestDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}

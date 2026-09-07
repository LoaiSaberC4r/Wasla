using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Wasla.Domain.Doctors;
using Wasla.Domain.Security;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Tests.Integration;

public sealed class PersistenceModelTests
{
    private static IModel CreateModel()
    {
        var options = new DbContextOptionsBuilder<WaslaDbContext>()
            .UseSqlServer("Server=localhost;Database=WaslaModel;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new WaslaDbContext(options);
        return context.Model;
    }

    [Fact]
    public void SecurityIndexes_AreUniqueAndTargeted()
    {
        var model = CreateModel();
        AssertUniqueIndex<ApplicationUser>(model, nameof(ApplicationUser.UserName));
        AssertUniqueIndex<ApplicationUser>(model, nameof(ApplicationUser.Email));
        AssertUniqueIndex<Role>(model, nameof(Role.Name));
        AssertUniqueIndex<Permission>(model, nameof(Permission.Name));
        AssertUniqueIndex<RolePermission>(model, nameof(RolePermission.RoleId), nameof(RolePermission.PermissionId));
        AssertUniqueIndex<UserRole>(model, nameof(UserRole.ApplicationUserId), nameof(UserRole.RoleId));
        AssertUniqueIndex<SuperAdmin>(model, nameof(SuperAdmin.ApplicationUserId));
    }

    [Fact]
    public void DoctorAndRecovery_UseRowVersionsAndFilteredUniqueIndexes()
    {
        var model = CreateModel();
        var doctor = model.FindEntityType(typeof(Doctor))!;
        Assert.True(doctor.FindProperty(nameof(Doctor.RowVersion))!.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, doctor.FindProperty(nameof(Doctor.RowVersion))!.ValueGenerated);
        Assert.Contains(doctor.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Single().Name == nameof(Doctor.NationalId) &&
            index.GetFilter() == "[NationalId] IS NOT NULL");

        var challenge = model.FindEntityType(typeof(PasswordResetChallenge))!;
        Assert.True(challenge.FindProperty(nameof(PasswordResetChallenge.RowVersion))!.IsConcurrencyToken);
        Assert.Contains(challenge.GetIndexes(), index =>
            index.IsUnique &&
            index.GetFilter() == "[InvalidatedOnUtc] IS NULL AND [ConsumedOnUtc] IS NULL");
    }

    private static void AssertUniqueIndex<TEntity>(IModel model, params string[] propertyNames)
    {
        var entity = model.FindEntityType(typeof(TEntity))!;
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(propertyNames));
    }
}

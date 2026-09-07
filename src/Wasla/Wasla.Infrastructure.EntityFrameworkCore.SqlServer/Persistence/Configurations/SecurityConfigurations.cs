using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration : IWriteEntityConfiguration<ApplicationUser>
{
    public void ConfigureAggregate(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("ApplicationUsers");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.UserName).HasMaxLength(100).IsRequired();
        builder.Property(user => user.Email).HasMaxLength(200).IsRequired();
        builder.Property(user => user.PhoneNumber).HasMaxLength(30);
        builder.Property(user => user.PasswordHash).HasMaxLength(1000).IsRequired();
        builder.Property(user => user.UserType).HasConversion<int>().IsRequired();
        builder.Property(user => user.IsActive).IsRequired();
        builder.Property(user => user.IsFirstLogin).IsRequired();
        builder.Property(user => user.PasswordChangedOnUtc).IsRequired();
        builder.HasIndex(user => user.UserName).IsUnique().HasDatabaseName("UX_ApplicationUsers_UserName");
        builder.HasIndex(user => user.Email).IsUnique().HasDatabaseName("UX_ApplicationUsers_Email");
        builder.HasIndex(user => user.PhoneNumber).HasDatabaseName("IX_ApplicationUsers_PhoneNumber");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(user => user.CreatedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RoleConfiguration : IWriteEntityConfiguration<Role>
{
    public void ConfigureAggregate(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Name).HasMaxLength(150).IsRequired();
        builder.HasIndex(role => role.Name).IsUnique().HasDatabaseName("UX_Roles_Name");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(role => role.CreatedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PermissionConfiguration : IWriteEntityConfiguration<Permission>
{
    public void ConfigureAggregate(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");
        builder.HasKey(permission => permission.Id);
        builder.Property(permission => permission.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(permission => permission.Name).IsUnique().HasDatabaseName("UX_Permissions_Name");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(permission => permission.CreatedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RolePermissionConfiguration : IWriteEntityConfiguration<RolePermission>
{
    public void ConfigureAggregate(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");
        builder.HasKey(mapping => mapping.Id);
        builder.HasIndex(mapping => new { mapping.RoleId, mapping.PermissionId })
            .IsUnique()
            .HasDatabaseName("UX_RolePermissions_RoleId_PermissionId");
        builder.HasIndex(mapping => mapping.RoleId).HasDatabaseName("IX_RolePermissions_RoleId");
        builder.HasIndex(mapping => mapping.PermissionId).HasDatabaseName("IX_RolePermissions_PermissionId");
        builder.HasOne<Role>().WithMany().HasForeignKey(mapping => mapping.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Permission>().WithMany().HasForeignKey(mapping => mapping.PermissionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(mapping => mapping.CreatedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class UserRoleConfiguration : IWriteEntityConfiguration<UserRole>
{
    public void ConfigureAggregate(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");
        builder.HasKey(mapping => mapping.Id);
        builder.HasIndex(mapping => new { mapping.ApplicationUserId, mapping.RoleId })
            .IsUnique()
            .HasDatabaseName("UX_UserRoles_ApplicationUserId_RoleId");
        builder.HasIndex(mapping => mapping.ApplicationUserId).HasDatabaseName("IX_UserRoles_ApplicationUserId");
        builder.HasIndex(mapping => mapping.RoleId).HasDatabaseName("IX_UserRoles_RoleId");
        builder.HasOne<ApplicationUser>()
            .WithMany(user => user.UserRoles)
            .HasForeignKey(mapping => mapping.ApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Role>().WithMany().HasForeignKey(mapping => mapping.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(mapping => mapping.CreatedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SuperAdminConfiguration : IWriteEntityConfiguration<SuperAdmin>
{
    public void ConfigureAggregate(EntityTypeBuilder<SuperAdmin> builder)
    {
        builder.ToTable("SuperAdmins");
        builder.HasKey(admin => admin.Id);
        builder.Property(admin => admin.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(admin => admin.NameEn).HasMaxLength(200);
        builder.HasIndex(admin => admin.ApplicationUserId)
            .IsUnique()
            .HasDatabaseName("UX_SuperAdmins_ApplicationUserId");
        builder.HasIndex(admin => admin.IsRootSuperAdmin)
            .IsUnique()
            .HasFilter("[IsRootSuperAdmin] = 1")
            .HasDatabaseName("UX_SuperAdmins_OneRoot");
        builder.HasOne(admin => admin.ApplicationUser)
            .WithOne()
            .HasForeignKey<SuperAdmin>(admin => admin.ApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(admin => admin.CreatedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PasswordResetChallengeConfiguration : IWriteEntityConfiguration<PasswordResetChallenge>
{
    public void ConfigureAggregate(EntityTypeBuilder<PasswordResetChallenge> builder)
    {
        builder.ToTable("PasswordResetChallenges");
        builder.HasKey(challenge => challenge.Id);
        builder.Property(challenge => challenge.OtpHash).HasMaxLength(128).IsRequired();
        builder.Property(challenge => challenge.ResetTokenHash).HasMaxLength(128);
        builder.Property(challenge => challenge.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(challenge => challenge.ApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(challenge => new { challenge.ApplicationUserId, challenge.CreatedOnUtc })
            .HasDatabaseName("IX_PasswordResetChallenges_ApplicationUserId_CreatedOnUtc");
        builder.HasIndex(challenge => challenge.ExpiresOnUtc)
            .HasDatabaseName("IX_PasswordResetChallenges_ExpiresOnUtc");
        builder.HasIndex(challenge => challenge.ApplicationUserId)
            .IsUnique()
            .HasFilter("[InvalidatedOnUtc] IS NULL AND [ConsumedOnUtc] IS NULL")
            .HasDatabaseName("UX_PasswordResetChallenges_Current_ApplicationUserId");
    }
}


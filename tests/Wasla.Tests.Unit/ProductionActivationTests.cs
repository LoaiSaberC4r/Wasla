using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Application.Features.Families;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;

namespace Wasla.Tests.Unit;

public sealed class ProductionActivationTests
{
    private static readonly string ContentRoot = Path.Combine(Path.GetTempPath(), "WaslaProductionActivationTests");

    private static IConfigurationRoot Configuration(Dictionary<string, string?>? overrides = null)
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Wasla.sln"))) folder = folder.Parent;
        return new ConfigurationBuilder().SetBasePath(Path.Combine(folder!.FullName, "src/Wasla/Wasla.Api"))
            .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Production.json")
            .AddInMemoryCollection(overrides ?? []).Build();
    }
    [Fact]
    public void Production_archive_is_enabled_and_retains_inherited_batch_settings()
    {
        var configuration = Configuration();
        Assert.True(configuration.GetValue<bool>("Archive:Enabled"));
        Assert.True(configuration.GetValue<bool>("Archive:ReadModelsVerified"));
        Assert.Equal(50, configuration.GetValue<int>("Archive:BatchSize"));
        Assert.Equal(300, configuration.GetValue<int>("Archive:IntervalSeconds"));
    }
    [Fact]
    public void Production_reservation_emails_are_owned_by_notifications_with_outbox_enabled()
    {
        var configuration = Configuration();
        Assert.True(configuration.GetValue<bool>("Notifications:Enabled"));
        Assert.True(configuration.GetValue<bool>("Notifications:DispatchEnabled"));
        Assert.True(configuration.GetValue<bool>("Notifications:OwnReservationEmails"));
        Assert.True(configuration.GetValue<bool>("EmailOutbox:Enabled"));
    }
    [Fact]
    public void Owner_approved_policy_is_complete_and_production_storage_uses_portable_private_paths()
    {
        var configuration = Configuration(); var policy = configuration.GetSection("FamilyMedicalPolicy").Get<FamilyMedicalPolicyOptions>()!;
        Assert.True(policy.IsConfigured); Assert.Equal("WASLA-P16-FAMILY-MEDICAL-v1.0", policy.PolicyVersion);
        Assert.Equal("WASLA-P16-PRODUCT-OWNER-20261015", policy.ApprovalReference); Assert.Equal("EG", policy.Jurisdiction);
        Assert.True(policy.RestrictionEvidenceRequired); Assert.True(policy.RestorationEvidenceRequired); Assert.Equal(5, policy.RestrictionGroundsReferences.Length);
        Assert.Empty(ProductionActivationValidation.Errors(configuration, ContentRoot));
        foreach (var key in new[] { "MediaStorage:RootPath", "MedicalRecord:DataProtectionKeyRingPath", "MedicalRecord:DataProtectionCertificatePath" })
        {
            Assert.False(Path.IsPathRooted(configuration[key]));
            var resolved = PrivateStoragePathResolver.Resolve(ContentRoot, configuration[key]);
            Assert.True(Path.IsPathFullyQualified(resolved));
            Assert.StartsWith(Path.Combine(ContentRoot, "App_Data", "Wasla") + Path.DirectorySeparatorChar, resolved,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
    }
    [Theory]
    [InlineData("App_Data/Wasla/Media", "Media")]
    [InlineData("App_Data\\Wasla\\Media", "Media")]
    [InlineData("App_Data/Wasla\\Media", "Media")]
    [InlineData("App_Data/Wasla/Media/../DataProtection/KeyRing", "DataProtection/KeyRing")]
    [InlineData("App_Data/Wasla/Certificates/Wasla-DataProtection.pfx", "Certificates/Wasla-DataProtection.pfx")]
    public void Private_storage_paths_are_normalized_against_content_root(string configured, string relative)
        => Assert.Equal(Path.Combine(ContentRoot, "App_Data", "Wasla", relative.Replace('/', Path.DirectorySeparatorChar)),
            PrivateStoragePathResolver.Resolve(ContentRoot, configured));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("/var/private")]
    [InlineData("C:\\private")]
    [InlineData("C:private")]
    [InlineData("\\\\server\\share")]
    [InlineData("\\private")]
    [InlineData("../../outside")]
    [InlineData("..\\..\\outside")]
    [InlineData("App_Data/Wasla/../../../outside")]
    [InlineData("App_Data\\Wasla\\..\\..\\outside")]
    [InlineData("App_Data/Wasla/../Wasla-other/Media")]
    [InlineData("App_Data/WaslaBackup/Media")]
    [InlineData("App_Data/Wasla/.. /outside")]
    [InlineData("App_Data/Wasla/Media:stream")]
    [InlineData("App_Data/Wasla/Media\0")]
    public void Private_storage_rejects_empty_rooted_and_escaping_paths_without_disclosing_paths(string? configured)
    {
        var error = Assert.Throws<ArgumentException>(() => PrivateStoragePathResolver.Resolve(ContentRoot, configured));
        Assert.DoesNotContain(ContentRoot, error.Message, StringComparison.Ordinal);
        var errors = ProductionActivationValidation.Errors(Configuration(new() { ["MediaStorage:RootPath"] = configured }), ContentRoot);
        Assert.Contains(errors, e => e.StartsWith("MediaStorage:RootPath", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData("MedicalRecord:DataProtectionKeyRingPath")]
    [InlineData("MedicalRecord:DataProtectionCertificatePath")]
    public void Missing_production_protection_paths_cannot_fall_back_to_default_keys(string key)
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => services.AddWaslaSqlServerPersistence(Configuration(new() { [key] = null }), ContentRoot, isProduction: true));
    }
    [Theory]
    [InlineData("DependentManagement:Enabled")][InlineData("HistoricalDocuments:Enabled")][InlineData("MedicalRecord:GuardianSharingEnabled")]
    [InlineData("MedicalRecord:CrossDoctorSharingEnabled")][InlineData("MalwareScanner:Enabled")][InlineData("PrivateMediaReconciliation:Enabled")]
    [InlineData("NotificationPrivacy:ClinicalEmailReleaseApproved")][InlineData("NotificationPrivacy:ClinicalInAppReleaseApproved")][InlineData("EmailOutbox:Enabled")]
    public void Disabled_production_feature_is_a_startup_error(string key)
        => Assert.Contains(ProductionActivationValidation.Errors(Configuration(new() { [key] = "false" }), ContentRoot), e => e.StartsWith(key, StringComparison.Ordinal));
    [Fact]
    public void Automatic_production_migrations_and_invalid_policy_never_pass_validation()
    {
        var errors = ProductionActivationValidation.Errors(Configuration(new() { ["DatabaseInitialization:ApplyMigrationsOnStartup"] = "true", ["FamilyMedicalPolicy:ApprovalReference"] = "" }), ContentRoot);
        Assert.Contains(errors, e => e.StartsWith("DatabaseInitialization", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("FamilyMedicalPolicy", StringComparison.Ordinal));
    }
}

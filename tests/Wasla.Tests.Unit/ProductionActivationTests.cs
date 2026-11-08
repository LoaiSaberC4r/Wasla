using Microsoft.Extensions.Configuration;
using Wasla.Application.Features.Families;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;

namespace Wasla.Tests.Unit;

public sealed class ProductionActivationTests
{
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
    public void Owner_approved_policy_is_complete_and_only_real_host_paths_remain_unconfirmed()
    {
        var configuration = Configuration(); var policy = configuration.GetSection("FamilyMedicalPolicy").Get<FamilyMedicalPolicyOptions>()!;
        Assert.True(policy.IsConfigured); Assert.Equal("WASLA-P16-FAMILY-MEDICAL-v1.0", policy.PolicyVersion);
        Assert.Equal("WASLA-P16-PRODUCT-OWNER-20261015", policy.ApprovalReference); Assert.Equal("EG", policy.Jurisdiction);
        Assert.True(policy.RestrictionEvidenceRequired); Assert.True(policy.RestorationEvidenceRequired); Assert.Equal(5, policy.RestrictionGroundsReferences.Length);
        var errors = ProductionActivationValidation.Errors(configuration);
        Assert.Equal(3, errors.Count); Assert.All(errors, e => Assert.Contains("requires a confirmed absolute persistent host path", e, StringComparison.Ordinal));
    }
    [Theory]
    [InlineData("DependentManagement:Enabled")][InlineData("HistoricalDocuments:Enabled")][InlineData("MedicalRecord:GuardianSharingEnabled")]
    [InlineData("MedicalRecord:CrossDoctorSharingEnabled")][InlineData("MalwareScanner:Enabled")][InlineData("PrivateMediaReconciliation:Enabled")]
    [InlineData("NotificationPrivacy:ClinicalEmailReleaseApproved")][InlineData("NotificationPrivacy:ClinicalInAppReleaseApproved")][InlineData("EmailOutbox:Enabled")]
    public void Disabled_production_feature_is_a_startup_error(string key)
        => Assert.Contains(ProductionActivationValidation.Errors(Configuration(new() { [key] = "false" })), e => e.StartsWith(key, StringComparison.Ordinal));
    [Fact]
    public void Automatic_production_migrations_and_invalid_policy_never_pass_validation()
    {
        var errors = ProductionActivationValidation.Errors(Configuration(new() { ["DatabaseInitialization:ApplyMigrationsOnStartup"] = "true", ["FamilyMedicalPolicy:ApprovalReference"] = "" }));
        Assert.Contains(errors, e => e.StartsWith("DatabaseInitialization", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("FamilyMedicalPolicy", StringComparison.Ordinal));
    }
}

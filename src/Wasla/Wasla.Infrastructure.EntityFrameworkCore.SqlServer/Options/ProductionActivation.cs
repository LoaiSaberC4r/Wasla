using System.Text;
using BuildingBlock.Infrastructure.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Wasla.Application.Features.Families;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Media;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;

public static class ProductionActivationValidation
{
    public static IReadOnlyList<string> Errors(IConfiguration configuration, string? contentRootPath = null)
    {
        var errors = new List<string>();
        foreach (var key in new[] { "MedicalRecord:CrossDoctorSharingEnabled", "MedicalRecord:GuardianSharingEnabled", "DependentManagement:Enabled",
            "HistoricalDocuments:Enabled", "PrivateMediaReconciliation:Enabled",
            "NotificationPrivacy:ClinicalEmailReleaseApproved", "NotificationPrivacy:ClinicalInAppReleaseApproved",
            "Notifications:Enabled", "Notifications:DispatchEnabled", "EmailOutbox:Enabled" })
            if (!configuration.GetValue<bool>(key)) errors.Add(key + " must be true.");
        if (configuration.GetValue<bool>("DatabaseInitialization:ApplyMigrationsOnStartup") || configuration.GetValue<bool>("DatabaseInitialization:ApplySeedingOnStartup"))
            errors.Add("DatabaseInitialization startup migration and seeding must remain false.");
        var policy = configuration.GetSection("FamilyMedicalPolicy").Get<FamilyMedicalPolicyOptions>() ?? new();
        if (!policy.IsConfigured) errors.Add("FamilyMedicalPolicy must contain approved policy metadata and allowed evidence.");
        if (!policy.RestrictionEvidenceRequired || !policy.RestorationEvidenceRequired || policy.RestrictionGroundsReferences.Length == 0 ||
            policy.RestrictionGroundsReferences.Any(string.IsNullOrWhiteSpace)) errors.Add("FamilyMedicalPolicy restriction/restoration evidence and approved grounds are required.");
        var scanner = configuration.GetSection("MalwareScanner").Get<ClamAvMalwareScannerOptions>() ?? new();
        if (scanner.Enabled && (!scanner.IsValid || scanner.MaxStreamBytes < configuration.GetValue<long>("HistoricalDocuments:MaxFileBytes")))
            errors.Add("MalwareScanner requires a loopback endpoint and sufficient stream limit.");
        var secret = configuration["MedicalRecord:OpaqueEntryKey"];
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32) errors.Add("MedicalRecord:OpaqueEntryKey requires at least 32 stable cryptographic bytes.");
        foreach (var key in new[] { "MedicalRecord:DataProtectionKeyRingPath", "MedicalRecord:DataProtectionCertificatePath", "MediaStorage:RootPath" })
        {
            try { PrivateStoragePathResolver.Resolve(contentRootPath ?? AppContext.BaseDirectory, configuration[key]); }
            catch (ArgumentException) { errors.Add(key + " requires a relative path inside App_Data/Wasla."); }
        }
        var smtp = configuration.GetSection("Smtp").Get<SmtpOptions>() ?? new();
        if (string.IsNullOrWhiteSpace(smtp.Host) || smtp.Port is < 1 or > 65535 || !smtp.RequireStartTls || smtp.UseSsl ||
            string.IsNullOrWhiteSpace(smtp.UserName) || string.IsNullOrWhiteSpace(smtp.Password) || !System.Net.Mail.MailAddress.TryCreate(smtp.FromEmail, out _))
            errors.Add("Smtp requires authenticated STARTTLS configuration.");
        return errors;
    }
}

internal sealed class ProductionActivationStartup(IConfiguration configuration, IHostEnvironment environment,
    ClamAvMalwareScanner scanner, IDataProtectionProvider protection) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsProduction()) return;
        var errors = ProductionActivationValidation.Errors(configuration, environment.ContentRootPath);
        if (errors.Count != 0) throw new InvalidOperationException("Production activation configuration: " + string.Join(" ", errors));
        PrivateStorageWriteProbe.Verify(PrivateStoragePathResolver.Resolve(environment.ContentRootPath, configuration["MediaStorage:RootPath"]));
        PrivateStorageWriteProbe.Verify(PrivateStoragePathResolver.Resolve(environment.ContentRootPath, configuration["MedicalRecord:DataProtectionKeyRingPath"]));
        var test = protection.CreateProtector("Wasla.ProductionReadiness.v1");
        if (test.Unprotect(test.Protect("readiness")) != "readiness") throw new InvalidOperationException("Persistent Data Protection verification failed.");
        if (configuration.GetValue<bool>("MalwareScanner:Enabled") && !await scanner.PingAsync(cancellationToken))
            throw new InvalidOperationException("MalwareScanner dependency is unreachable or unhealthy.");
        var smtp = configuration.GetSection("Smtp").Get<SmtpOptions>()!;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            using var client = new MailKit.Net.Smtp.SmtpClient { Timeout = Math.Min(smtp.TimeoutMilliseconds, 30000) };
            await client.ConnectAsync(smtp.Host, smtp.Port, MailKit.Security.SecureSocketOptions.StartTls, timeout.Token);
            await client.AuthenticateAsync(smtp.UserName, smtp.Password, timeout.Token);
            await client.DisconnectAsync(true, timeout.Token);
        }
        catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        { throw new InvalidOperationException("Smtp authenticated STARTTLS startup probe failed. Verify the existing appsettings credentials and network access."); }
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class PrivateDependencyHealthCheck(IHostEnvironment environment, IConfiguration configuration,
    ClamAvMalwareScanner scanner) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!environment.IsProduction()) return HealthCheckResult.Healthy();
        if (ProductionActivationValidation.Errors(configuration, environment.ContentRootPath).Count != 0) return HealthCheckResult.Unhealthy("Production configuration is incomplete.");
        try
        {
            PrivateStorageWriteProbe.Verify(PrivateStoragePathResolver.Resolve(environment.ContentRootPath, configuration["MediaStorage:RootPath"]));
            PrivateStorageWriteProbe.Verify(PrivateStoragePathResolver.Resolve(environment.ContentRootPath, configuration["MedicalRecord:DataProtectionKeyRingPath"]));
            using var certificate = File.OpenRead(PrivateStoragePathResolver.Resolve(environment.ContentRootPath, configuration["MedicalRecord:DataProtectionCertificatePath"]));
            if (certificate.ReadByte() == -1) return HealthCheckResult.Unhealthy("Private storage or certificate access is unavailable.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return HealthCheckResult.Unhealthy("Private storage or certificate access is unavailable.");
        }
        if (!configuration.GetValue<bool>("MalwareScanner:Enabled"))
            return HealthCheckResult.Degraded("Malware scanning is temporarily disabled.");
        return await scanner.PingAsync(cancellationToken) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Private media scanner unavailable.");
    }
}

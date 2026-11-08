using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Clinical;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Media;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;

namespace Wasla.Tests.Unit;

public sealed class ProductionScannerDependencyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WaslaScannerDependency_" + Guid.NewGuid().ToString("N"));
    private const string MediaPath = "App_Data/Wasla/Media";
    private const string KeyPath = "App_Data/Wasla/DataProtection/KeyRing";
    private const string CertificatePath = "App_Data/Wasla/Certificates/test.pfx";

    public ProductionScannerDependencyTests()
    {
        Directory.CreateDirectory(Resolve(MediaPath));
        Directory.CreateDirectory(Resolve(KeyPath));
        Directory.CreateDirectory(Path.GetDirectoryName(Resolve(CertificatePath))!);
        // The health check verifies readability only; no real certificate is needed here.
        File.WriteAllText(Resolve(CertificatePath), "synthetic health-check certificate bytes");
    }

    [Fact]
    public void Production_historical_options_allow_optional_scanning_and_still_validate_limits()
    {
        var options = Configuration().GetSection("HistoricalDocuments").Get<HistoricalDocumentOptions>()!;
        Assert.False(options.RequireRealScanner);
        Assert.True(new HistoricalDocumentOptionsValidator().Validate(null, options).Succeeded);
        Assert.True(options.IsValid());
        options.MaxFileBytes = 0;
        Assert.False(options.IsValid());
    }

    [Fact]
    public async Task Disabled_scanner_reports_degraded_without_accessing_the_scanner()
    {
        var check = new PrivateDependencyHealthCheck(Environment(), Configuration(), new ClamAvMalwareScanner(new UnusedScannerOptions()));
        var result = await check.CheckHealthAsync(new(), TestContext.Current.CancellationToken);
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal("Malware scanning is temporarily disabled.", result.Description);
    }

    [Theory]
    [InlineData(MediaPath)] [InlineData(KeyPath)] [InlineData(CertificatePath)]
    public async Task Disabled_scanning_still_reports_missing_private_dependencies_as_unhealthy(string path)
    {
        if (path == CertificatePath) File.Delete(Resolve(path));
        else Directory.Delete(Resolve(path));
        var check = new PrivateDependencyHealthCheck(Environment(), Configuration(), new ClamAvMalwareScanner(new UnusedScannerOptions()));
        var result = await check.CheckHealthAsync(new(), TestContext.Current.CancellationToken);
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Private storage or certificate access is unavailable.", result.Description);
    }

    [Fact]
    public async Task Disabled_scanning_still_reports_invalid_production_configuration_as_unhealthy()
    {
        var configuration = Configuration(); configuration["MedicalRecord:OpaqueEntryKey"] = "short";
        var check = new PrivateDependencyHealthCheck(Environment(), configuration, new ClamAvMalwareScanner(new UnusedScannerOptions()));
        var result = await check.CheckHealthAsync(new(), TestContext.Current.CancellationToken);
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Production configuration is incomplete.", result.Description);
    }

    [Theory]
    [InlineData("PONG\0", HealthStatus.Healthy)]
    [InlineData("FAIL\0", HealthStatus.Unhealthy)]
    public async Task Enabled_scanning_health_uses_the_existing_ClamAV_ping(string reply, HealthStatus expected)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var ct = TestContext.Current.CancellationToken;
        var configuration = Configuration(true, ((IPEndPoint)listener.LocalEndpoint).Port);
        var check = new PrivateDependencyHealthCheck(Environment(), configuration, Scanner(configuration));
        var pending = check.CheckHealthAsync(new(), ct);
        using var client = await listener.AcceptTcpClientAsync(ct);
        await using var socket = client.GetStream();
        var command = new byte[6]; await socket.ReadExactlyAsync(command, ct);
        Assert.Equal("zPING\0", Encoding.ASCII.GetString(command));
        await socket.WriteAsync(Encoding.ASCII.GetBytes(reply), ct);
        Assert.Equal(expected, (await pending).Status);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Production_startup_without_ClamAV_only_requires_it_when_enabled(bool enabled)
    {
        using var unavailable = new TcpListener(IPAddress.Loopback, 0); unavailable.Start();
        var port = ((IPEndPoint)unavailable.LocalEndpoint).Port; unavailable.Stop();
        var configuration = Configuration(enabled, port);
        configuration["Smtp:Port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var scanner = enabled ? Scanner(configuration) : new ClamAvMalwareScanner(new UnusedScannerOptions());
        var startup = new ProductionActivationStartup(configuration, Environment(), scanner, new EphemeralDataProtectionProvider());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => startup.StartAsync(TestContext.Current.CancellationToken));
        // An unavailable synthetic SMTP endpoint proves disabled scanning reaches the next independent startup probe.
        Assert.StartsWith(enabled ? "MalwareScanner dependency" : "Smtp authenticated STARTTLS startup probe", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(MediaPath)] [InlineData(KeyPath)]
    public async Task Disabled_scanning_preserves_startup_storage_probes(string path)
    {
        Directory.Delete(Resolve(path));
        var startup = new ProductionActivationStartup(Configuration(), Environment(), new ClamAvMalwareScanner(new UnusedScannerOptions()), new EphemeralDataProtectionProvider());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => startup.StartAsync(TestContext.Current.CancellationToken));
        Assert.DoesNotContain("Smtp", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("MalwareScanner", error.Message, StringComparison.Ordinal);
    }

    private static IConfigurationRoot Configuration(bool enabled = false, int port = 3310)
        => ProductionActivationTests.Configuration(new()
        {
            ["MediaStorage:RootPath"] = MediaPath,
            ["MedicalRecord:DataProtectionKeyRingPath"] = KeyPath,
            ["MedicalRecord:DataProtectionCertificatePath"] = CertificatePath,
            ["MalwareScanner:Enabled"] = enabled.ToString(),
            ["MalwareScanner:Port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["MalwareScanner:TimeoutSeconds"] = "1",
            ["Smtp:Host"] = "127.0.0.1", ["Smtp:UserName"] = "synthetic", ["Smtp:Password"] = "synthetic",
            ["Smtp:FromEmail"] = "synthetic@example.test"
        });

    private static ClamAvMalwareScanner Scanner(IConfiguration configuration)
        => new(Options.Create(configuration.GetSection("MalwareScanner").Get<ClamAvMalwareScannerOptions>()!));

    private ProductionEnvironment Environment() => new() { ContentRootPath = _root };
    private string Resolve(string path) => PrivateStoragePathResolver.Resolve(_root, path);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private sealed class UnusedScannerOptions : IOptions<ClamAvMalwareScannerOptions>
    {
        public ClamAvMalwareScannerOptions Value => throw new InvalidOperationException("Disabled scanner must not be accessed.");
    }

    private sealed class ProductionEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Wasla.Tests.Unit";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

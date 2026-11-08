using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using BuildingBlock.Infrastructure.Options;
using Wasla.Infrastructure;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Media;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;

namespace Wasla.Tests.Integration;

public sealed class ProductionDependencyTests
{
    [Fact]
    public async Task Real_ClamAV_accepts_clean_bytes_and_detects_standard_EICAR_in_memory()
    {
        var scanner = new ClamAvMalwareScanner(Options.Create(new ClamAvMalwareScannerOptions { Enabled = true, Host = "127.0.0.1" }));
        Assert.True(await scanner.PingAsync(TestContext.Current.CancellationToken));
        await using var clean = new MemoryStream("%PDF-1.4\nsynthetic clean source\n%%EOF"u8.ToArray());
        Assert.True((await scanner.ScanAsync(clean, "clean.pdf", "application/pdf", TestContext.Current.CancellationToken)).IsSafe);
        // Build the standard harmless antivirus test signature without creating a local file.
        var signature = "X5O!P%@AP[4\\PZX54(P^)7CC)7}" + "$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";
        await using var eicar = new MemoryStream(Encoding.ASCII.GetBytes(signature));
        var result = await scanner.ScanAsync(eicar, "synthetic-test.txt", "text/plain", TestContext.Current.CancellationToken);
        Assert.False(result.IsSafe); Assert.Equal("MalwareDetected", result.Reason);
    }

    [Fact]
    public async Task Scanner_unavailable_and_timeout_fail_closed()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var scanner = new ClamAvMalwareScanner(Options.Create(new ClamAvMalwareScannerOptions { Enabled = true, Host = "127.0.0.1", Port = port, TimeoutSeconds = 1 }));
        var accepted = listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
        await using var bytes = new MemoryStream("synthetic bytes"u8.ToArray());
        var pending = scanner.ScanAsync(bytes, "test.txt", "text/plain", TestContext.Current.CancellationToken);
        using var socket = await accepted; Assert.False((await pending).IsSafe);
        listener.Stop(); bytes.Position = 0; Assert.False((await scanner.ScanAsync(bytes, "test.txt", "text/plain", TestContext.Current.CancellationToken)).IsSafe);
    }

    [Fact]
    public void Protected_key_ring_survives_restart_and_is_shared_by_independent_instances()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "WaslaDataProtection_" + Guid.NewGuid().ToString("N")));
        const string keyRingPath = "App_Data/Wasla/DataProtection/KeyRing";
        const string certificatePath = "App_Data/Wasla/Certificates/Wasla-DataProtection.pfx";
        const string mediaPath = "App_Data/Wasla/Media";
        const string testPassword = "synthetic-test-only";
        var keyRing = PrivateStoragePathResolver.Resolve(root, keyRingPath);
        var pfx = PrivateStoragePathResolver.Resolve(root, certificatePath);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = "Server=(local);Database=WaslaSyntheticStorageTest;Integrated Security=true",
            ["MedicalRecord:DataProtectionKeyRingPath"] = keyRingPath,
            ["MedicalRecord:DataProtectionCertificatePath"] = certificatePath,
            ["MedicalRecord:DataProtectionCertificatePassword"] = testPassword,
            ["MediaStorage:RootPath"] = mediaPath
        }).Build();
        ServiceProvider Instance()
        {
            var services = new ServiceCollection(); services.AddLogging();
            services.AddWaslaInfrastructure(configuration);
            services.AddWaslaSqlServerPersistence(configuration, root, isProduction: true);
            return services.BuildServiceProvider();
        }
        try
        {
            Directory.CreateDirectory(keyRing); Directory.CreateDirectory(Path.GetDirectoryName(pfx)!);
            using (var rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest("CN=Wasla synthetic test only", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
                File.WriteAllBytes(pfx, certificate.Export(X509ContentType.Pfx, testPassword));
            }
            string token;
            using (var first = Instance())
            {
                Assert.Equal(PrivateStoragePathResolver.Resolve(root, mediaPath), first.GetRequiredService<IOptions<MediaStorageOptions>>().Value.RootPath);
                token = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("Wasla.FamilyJoinInvitation.Response.v1").Protect("synthetic bound reference");
            }
            using var restarted = Instance(); using var secondNode = Instance();
            Assert.Equal("synthetic bound reference", restarted.GetRequiredService<IDataProtectionProvider>().CreateProtector("Wasla.FamilyJoinInvitation.Response.v1").Unprotect(token));
            Assert.Equal("synthetic bound reference", secondNode.GetRequiredService<IDataProtectionProvider>().CreateProtector("Wasla.FamilyJoinInvitation.Response.v1").Unprotect(token));
            var xml = File.ReadAllText(Assert.Single(Directory.GetFiles(keyRing, "key-*.xml")));
            Assert.Contains("encryptedSecret", xml, StringComparison.Ordinal); Assert.DoesNotContain("<value>", xml, StringComparison.Ordinal);
            Assert.Throws<System.Security.Cryptography.CryptographicException>(() => secondNode.GetRequiredService<IDataProtectionProvider>().CreateProtector("OtherPurpose").Unprotect(token));
            var keyFiles = Directory.GetFiles(keyRing).Order(StringComparer.Ordinal).ToArray();
            PrivateStorageWriteProbe.Verify(keyRing);
            Assert.Equal(keyFiles, Directory.GetFiles(keyRing).Order(StringComparer.Ordinal).ToArray());
            Assert.Empty(Directory.GetFiles(keyRing, "*.tmp"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
}

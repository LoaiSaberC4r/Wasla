using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Clinical;
using Wasla.Domain.Clinical;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase15DiagnosticApiTests;
using static Wasla.Tests.Integration.Phase16Fixture;

namespace Wasla.Tests.Integration;

public sealed class Phase16SharingActivationTests : IDisposable
{
    private readonly string _prefix = "WaslaPhase16_" + Guid.NewGuid().ToString("N") + "_";
    private readonly string _key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public Phase16SharingActivationTests()
        => Environment.SetEnvironmentVariable(_prefix + "MedicalRecord__OpaqueEntryKey", _key);

    public void Dispose() => Environment.SetEnvironmentVariable(_prefix + "MedicalRecord__OpaqueEntryKey", null);

    [Theory]
    [InlineData(null, true)]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    public void Configuration_binds_external_key_and_environment_release_gates(string? environment, bool enabled)
    {
        var builder = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile("appsettings.json");
        if (environment is not null) builder.AddJsonFile($"appsettings.{environment}.json");
        using var configuration = (ConfigurationRoot)builder.AddEnvironmentVariables(_prefix).Build();
        var services = new ServiceCollection();
        services.AddOptions<MedicalRecordOptions>().Bind(configuration.GetSection("MedicalRecord"));
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MedicalRecordOptions>>().Value;
        Assert.Equal(enabled, options.CrossDoctorSharingEnabled);
        Assert.Equal(enabled, options.GuardianSharingEnabled);
        Assert.True(options.OpaqueEntryKey == _key, "The external opaque key must bind without being logged.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("too-short")]
    public async Task Enabled_sharing_without_a_valid_key_denies_a_live_treating_doctor(string? key)
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync();
        var treating = await TreatingAsync(app);
        await using var node = app.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MedicalRecord:CrossDoctorSharingEnabled"] = "true",
                ["MedicalRecord:OpaqueEntryKey"] = key
            })));
        using var authenticated = await app.ClientAsync("other-doctor");
        using var doctor = node.CreateClient();
        doctor.DefaultRequestHeaders.Authorization = authenticated.DefaultRequestHeaders.Authorization;
        using var denied = await doctor.GetAsync(TreatingUrl(app, treating.Ticket) + "/overview", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }

    [Fact]
    public async Task Sql_configuration_activated_sharing_expires_and_denies_reads_after_ticket_completion()
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(true);
        await using var node = app.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddEnvironmentVariables(_prefix)));
        var options = node.Services.GetRequiredService<IOptions<MedicalRecordOptions>>().Value;
        Assert.True(options.CrossDoctorSharingEnabled);
        Assert.True(options.GuardianSharingEnabled);
        Assert.True(options.OpaqueEntryKey == _key, "The API must bind the external opaque key.");
        using var owner = await app.ClientAsync("doctor");
        using var reception = await app.ClientAsync("reception");
        using var authenticated = await app.ClientAsync("other-doctor");
        using var doctor = node.CreateClient();
        doctor.DefaultRequestHeaders.Authorization = authenticated.DefaultRequestHeaders.Authorization;
        await app.WithDbAsync(db =>
        {
            Assert.True(db.Database.IsSqlServer());
            return Task.CompletedTask;
        });
        var source = await app.StartAsync(owner, await app.WalkInAsync(reception));
        var sourceId = source.GetProperty("medicalEncounterId").GetGuid();
        await app.CompleteAsync(owner, source);
        var treating = await TreatingAsync(app);
        var url = TreatingUrl(app, treating.Ticket);
        await GetAsync(doctor, url + $"/encounters/{sourceId}");
        await HideAsync(app, owner, sourceId);
        using (var denied = await doctor.GetAsync(url + $"/encounters/{sourceId}", TestContext.Current.CancellationToken))
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        var hidden = (await GetAsync(doctor, url + "/timeline")).GetProperty("items")[0];
        var requested = await OkAsync(await PostAsync(doctor, url + "/access-requests", new
        {
            hiddenEncounterToken = hidden.GetProperty("hiddenEncounterToken").GetString(), reason = "Review prior treatment"
        }));
        var requestId = requested.GetProperty("requestId").GetGuid();
        await OkAsync(await PostAsync(owner, $"/api/v1/doctors/me/medical-record-access-requests/{requestId}/approve",
            new { rowVersion = requested.GetProperty("rowVersion").GetString() }));
        await GetAsync(doctor, url + $"/encounters/{sourceId}");
        var encounterUrl = $"/api/v1/doctors/me/practices/{app.OtherPracticeId}/encounters/{treating.Encounter}";
        var encounter = await GetAsync(doctor, encounterUrl);
        encounter = await OkAsync(await doctor.PatchAsJsonAsync(encounterUrl + "/clinical-notes",
            new { clinicalNotes = "Treatment finished", rowVersion = encounter.GetProperty("rowVersion").GetString() }, TestContext.Current.CancellationToken));
        var ticketUrl = $"/api/v1/practices/{app.OtherPracticeId}/tickets/{treating.Ticket}";
        var ticket = await GetAsync(doctor, ticketUrl);
        await OkAsync(await PostAsync(doctor, ticketUrl + "/complete", new
        {
            ticketRowVersion = ticket.GetProperty("rowVersion").GetString(), encounterRowVersion = encounter.GetProperty("rowVersion").GetString()
        }));
        foreach (var suffix in new[] { "/overview", "/timeline", $"/encounters/{sourceId}" })
        {
            using var denied = await doctor.GetAsync(url + suffix, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }
        Assert.Equal("Expired", (await GetAsync(doctor, url + $"/access-requests/{requestId}")).GetProperty("effectiveStatus").GetString());
        await app.WithDbAsync(async db => Assert.Equal(HiddenAccessRequestStatus.Expired,
            (await db.Set<HiddenEncounterAccessRequest>().SingleAsync(TestContext.Current.CancellationToken)).Status));
    }
}

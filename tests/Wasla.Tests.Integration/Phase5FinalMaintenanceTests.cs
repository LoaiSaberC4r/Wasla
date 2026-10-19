using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Common;
using Wasla.Domain.Patients;
namespace Wasla.Tests.Integration;
public sealed class Phase5FinalMaintenanceTests
{
    [Fact, Trait("Category", "SQLServerConcurrency")]
    public async Task Operator_cli_authenticates_fresh_credentials_and_audits_hold_and_release_without_http_route()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true); var id = Guid.NewGuid(); string? connection = null;
        await app.WithDbAsync(async db =>
        {
            connection = db.Database.GetConnectionString(); var now = app.Clock.UtcNow;
            var proof = new PatientOnboardingChallenge(Guid.NewGuid(), PatientOnboardingPurpose.Claim, "operator@example.test", null, null, "synthetic-hash", now, now.AddMinutes(10), 5); db.PatientOnboardingChallenges.Add(proof);
            db.PatientClaimRequests.Add(new(id, null, "operator@example.test", null, "هوية تجريبية", new(1990, 1, 1), Gender.Male, null, proof.Id, Guid.NewGuid().ToString(), "fingerprint", now)); await db.SaveChangesAsync();
        });
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Wasla.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var tool = Path.Combine(directory.FullName, "tools", "Wasla.PatientIdentityMaintenance", "bin", "Release", "net10.0", "Wasla.PatientIdentityMaintenance.dll");
        Assert.True(File.Exists(tool), "Build the solution including its maintenance tool in Release before native CLI acceptance.");
        foreach (var action in new[] { "hold", "release" })
        {
            var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add(tool); start.ArgumentList.Add(action); start.ArgumentList.Add(id.ToString()); start.ArgumentList.Add("Synthetic documented operator decision");
            start.Environment["WASLA_IDENTITY_MAINTENANCE_CONNECTION"] = connection;
            start.Environment["WASLA_IDENTITY_OPERATOR_IDENTIFIER"] = "root";
            start.Environment["WASLA_IDENTITY_OPERATOR_PASSWORD"] = "Phase13TestPass123!";
            using var process = Process.Start(start)!; var errors = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken); var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken); Assert.True(process.ExitCode == 0, await errors); Assert.Contains("committed and audited", await output);
            await app.WithDbAsync(async db => Assert.Equal(action == "hold", (await db.PatientClaimRequests.SingleAsync(c => c.Id == id)).IsUnderLegalHold));
        }
        await app.WithDbAsync(async db => { Assert.Equal(2, await db.PatientClaimHistories.CountAsync(h => h.ClaimRequestId == id)); Assert.Equal(2, await db.PatientAccountLinks.CountAsync()); });
    }
}

using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Application.Features.Clinical;
using Wasla.Domain.Clinical;
using Wasla.Domain.Tickets;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase16Fixture;

namespace Wasla.Tests.Integration;

public sealed class Phase16PerformanceTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Sql_thousand_visit_history_has_bounded_stable_mixed_pages_and_an_inspectable_query_plan()
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(true); EnableSharing(app);
        using var doctor = await app.ClientAsync("other-doctor");
        await app.WithDbAsync(async db =>
        {
            var now = app.Clock.UtcNow.AddSeconds(-1);
            for (var i = 1; i <= 1000; i++)
            {
                var t = Ticket.CreateWalkIn(new TicketCreationSnapshot(Guid.NewGuid(), app.DoctorId, app.PracticeId, app.PatientId, null,
                    app.Today, i, TicketSource.WalkIn, app.SegmentId, "Synthetic", "Synthetic", 0, app.ConsultationTypeId,
                    "NewConsultation", "Synthetic", "Synthetic", 300, "Africa/Cairo", now, now, CheckInMode.WalkIn, app.DoctorUserId, null)).Value;
                t.Call(app.DoctorUserId, now); t.StartVisit(app.DoctorUserId, now);
                var e = MedicalEncounter.Start(t, app.DoctorUserId, now).Value; e.UpdateClinicalNotes("Synthetic internal note", app.DoctorUserId, now);
                e.Complete(t, app.DoctorUserId, now, i % 2 == 0 ? EncounterVisibility.Hidden : EncounterVisibility.Visible); t.Complete(app.DoctorUserId, now);
                db.Tickets.Add(t); db.MedicalEncounters.Add(e);
            }
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        var treating = await TreatingAsync(app); var url = TreatingUrl(app, treating.Ticket);
        var started = Stopwatch.GetTimestamp(); var page = await GetAsync(doctor, url + "/timeline?pageSize=100");
        var firstPageMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var entries = new HashSet<string>(StringComparer.Ordinal); var pages = 0; var hidden = 0;
        while (true)
        {
            pages++; Assert.InRange(page.GetProperty("items").GetArrayLength(), 1, 100);
            foreach (var row in page.GetProperty("items").EnumerateArray())
            {
                Assert.True(entries.Add(row.GetProperty("entryId").GetString()!));
                if (row.GetProperty("type").GetString() == "HiddenEncounterPlaceholder") { hidden++; Assert.False(row.TryGetProperty("occurredAtUtc", out _)); }
            }
            if (!page.GetProperty("hasMore").GetBoolean()) break;
            page = await GetAsync(doctor, url + "/timeline?pageSize=100&cursor=" + Uri.EscapeDataString(page.GetProperty("nextCursor").GetString()!));
        }
        Assert.Equal(1000, entries.Count); Assert.Equal(500, hidden); Assert.Equal(10, pages);
        output.WriteLine("Synthetic 1,000 completed visits: first page {0:F1} ms; 10 stable pages of 100; 500 strictly redacted placeholders.", firstPageMs);
        Assert.InRange(firstPageMs, 0, 30000);

        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.WaslaDbContext>();
        var reader = scope.ServiceProvider.GetRequiredService<IMedicalRecordReadService>(); var readerType = reader.GetType();
        var recordScope = new MedicalRecordScope(treating.User, app.PatientId, MedicalRecordMode.TreatingDoctor, treating.Doctor, app.OtherPracticeId, treating.Ticket, treating.Encounter, true);
        var sources = readerType.GetMethod("Authorized", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(reader, [recordScope]);
        var query = (IQueryable)readerType.GetMethod("Candidates", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(reader, [sources])!;
        foreach (var (method, property) in new[] { ("OrderByDescending", "Time"), ("ThenBy", "Kind"), ("ThenBy", "Key") })
        {
            var parameter = Expression.Parameter(query.ElementType, "e"); var member = Expression.Property(parameter, property);
            var lambda = Expression.Lambda(member, parameter);
            query = query.Provider.CreateQuery(Expression.Call(typeof(Queryable), method, [query.ElementType, member.Type], query.Expression, Expression.Quote(lambda)));
        }
        query = query.Provider.CreateQuery(Expression.Call(typeof(Queryable), nameof(Queryable.Take), [query.ElementType], query.Expression, Expression.Constant(101)));
        var sql = query.ToQueryString(); Assert.Contains("TOP(", sql, StringComparison.Ordinal); Assert.DoesNotContain("[ClinicalNotes]", sql, StringComparison.Ordinal); Assert.DoesNotContain("[Notes]", sql, StringComparison.Ordinal);
        await dbContext.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        var connection = dbContext.Database.GetDbConnection(); await using var command = connection.CreateCommand();
        command.CommandText = "SET SHOWPLAN_XML ON"; await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        string plan;
        try { command.CommandText = sql; plan = (string)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!; }
        finally { command.CommandText = "SET SHOWPLAN_XML OFF"; await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken); }
        Assert.Contains("ShowPlanXML", plan, StringComparison.Ordinal);
        var workspace = FindWorkspace(); var directory = Path.Combine(workspace, "_local-data", "phase16-validation"); Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "timeline-plan.xml"), plan, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "performance.txt"), $"Synthetic visits=1000; pageSize=100; pages={pages}; placeholders={hidden}; firstPageMs={firstPageMs:F1}", TestContext.Current.CancellationToken);
    }
    private static string FindWorkspace()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Wasla.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Workspace unavailable.");
    }
}

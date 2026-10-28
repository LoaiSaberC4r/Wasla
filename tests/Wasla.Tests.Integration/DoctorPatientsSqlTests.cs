using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Features.DoctorPatients;
using Wasla.Domain.Common;
using Wasla.Domain.Patients;
using Wasla.Domain.Practices;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class DoctorPatientsSqlTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Archive_move_preserves_membership_and_large_directory_uses_bounded_sql_pages()
    {
        await using var f = await Phase13ApiFixture.CreateAsync(true); using var doctor = await f.ClientAsync("doctor"); using var reception = await f.ClientAsync("reception");
        await f.CompleteAsync(doctor, await f.StartAsync(doctor, await f.WalkInAsync(reception)));
        var before = await GetAsync(doctor, "/api/v1/doctors/me/patients");
        f.Clock.UtcNow = f.Clock.UtcNow.AddDays(1);
        var flags = f.Services.GetRequiredService<IOptions<ArchiveOptions>>().Value; flags.Enabled = true; flags.ReadModelsVerified = true;
        using (var scope = f.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ArchiveOperationalDays>().ProcessAsync(TestContext.Current.CancellationToken);
            await scope.ServiceProvider.GetRequiredService<ArchiveEngine>().ProcessAsync(TestContext.Current.CancellationToken);
        }
        var after = await GetAsync(doctor, "/api/v1/doctors/me/patients");
        Assert.Equal(before.GetProperty("items")[0].GetProperty("attendanceCount").GetInt32(), after.GetProperty("items")[0].GetProperty("attendanceCount").GetInt32());
        var history = await GetAsync(doctor, $"/api/v1/doctors/me/patients/{f.PatientId}/attendances");
        Assert.True(history.GetProperty("items")[0].GetProperty("isArchived").GetBoolean());
        var seed = Stopwatch.StartNew();
        await f.WithDbAsync(async db =>
        {
            var second = Guid.NewGuid(); var segment = Guid.NewGuid(); var visit = Guid.NewGuid();
            db.DoctorPractices.Add(DoctorPractice.Create(second, f.DoctorId, "العيادة التاريخية", "Historical", 1, 1, 1, "Address", 30, 31, f.DoctorUserId).Value);
            db.DoctorPracticeConfigurations.Add(DoctorPracticeConfiguration.CreateDefault(Guid.NewGuid(), second, f.DoctorUserId).Value);
            db.DoctorPracticeSegments.Add(DoctorPracticeSegment.CreateDefault(segment, second, f.DoctorUserId).Value);
            db.DoctorPracticeVisitTypes.Add(DoctorPracticeVisitType.CreateDefault(visit, second, DoctorPracticeVisitTypeCode.NewConsultation, f.DoctorUserId).Value);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken); db.ChangeTracker.Clear();
            for (var i = 0; i < 2000; ++i)
            {
                var patient = Patient.Create(Guid.NewGuid(), "مريض اصطناعي " + i, "Synthetic-" + i, new(1990, 1, 1), Gender.Male, null, null, null, null, null, f.Today).Value;
                db.Patients.Add(patient);
                for (var j = 0; j < 10; ++j)
                {
                    var time = f.Clock.UtcNow.AddSeconds(-1 - i * 10 - j); var original = j % 2 == 0;
                    var ticket = Ticket.CreateWalkIn(new(Guid.NewGuid(), f.DoctorId, original ? f.PracticeId : second, patient.Id, null, f.Today,
                        i * 10 + j + 2, TicketSource.WalkIn, original ? f.SegmentId : segment, "Synthetic", "Synthetic", 0,
                        original ? f.ConsultationTypeId : visit, "NewConsultation", "Synthetic", "Synthetic", 300, "Africa/Cairo",
                        time, time, CheckInMode.WalkIn, f.DoctorUserId, null)).Value;
                    ticket.Cancel(f.DoctorUserId, "Synthetic cancellation after admission", time); db.Tickets.Add(ticket);
                }
                if ((i + 1) % 50 == 0) { await db.SaveChangesAsync(TestContext.Current.CancellationToken); db.ChangeTracker.Clear(); }
            }
        });
        seed.Stop();
        using var readerScope = f.Services.CreateScope(); var registered = readerScope.ServiceProvider.GetRequiredService<WaslaDbContext>();
        var probe = new QueryProbe();
        await using var db = new WaslaDbContext(new DbContextOptionsBuilder<WaslaDbContext>().UseSqlServer(registered.Database.GetConnectionString()).AddInterceptors(probe).Options, f.Clock);
        var type = typeof(WaslaDbContext).Assembly.GetType("Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.DoctorPatientReadService")!;
        var reader = (IDoctorPatientReadService)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db, f.Clock], null)!;
        var actor = new DoctorPatientActor(f.DoctorUserId, f.DoctorId, PermissionNames.All);
        var start = Stopwatch.GetTimestamp(); var page = await reader.DirectoryAsync(actor, new(PageSize: 100), TestContext.Current.CancellationToken);
        var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        Assert.Equal(2001, page.TotalCount); Assert.Equal(100, page.Items.Count); Assert.Equal(5, probe.Count);
        Assert.Contains("GROUP BY", probe.Sql, StringComparison.Ordinal); Assert.Contains("FETCH NEXT", probe.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ClinicalNotes", probe.Sql, StringComparison.Ordinal); Assert.DoesNotContain("PersonalId", probe.Sql, StringComparison.Ordinal);
        var seen = page.Items.Select(p => p.PatientId).ToHashSet();
        var capturedSql = probe.Sql;
        // New check-in after page one must not move the patient's earlier row into a repeated page.
        f.Clock.UtcNow = f.Clock.UtcNow.AddMinutes(1); await f.WalkInAsync(reception);
        for (var number = 2; seen.Count < page.TotalCount; ++number)
        {
            var next = await reader.DirectoryAsync(actor, new(PageNumber: number, PageSize: 100, AsOfUtc: page.AsOfUtc), TestContext.Current.CancellationToken);
            Assert.NotEmpty(next.Items); Assert.All(next.Items, p => Assert.True(seen.Add(p.PatientId)));
        }
        Assert.Equal(2001, seen.Count);
        var searched = await reader.DirectoryAsync(actor, new(Search: "Synthetic-1999", PageSize: 100), TestContext.Current.CancellationToken);
        Assert.Single(searched.Items); Assert.Equal(10, searched.Items[0].AttendanceCount); Assert.Equal(2, searched.Items[0].PracticeCount);
        var mixed = (ClinicalPage<DoctorPatientAttendance>)await reader.HistoryAsync(actor, new(f.PatientId, DoctorPatientHistory.Attendances), TestContext.Current.CancellationToken);
        Assert.Equal(2, mixed.TotalCount); Assert.Single(mixed.Items, t => t.IsArchived); Assert.Single(mixed.Items, t => !t.IsArchived);
        var folder = new DirectoryInfo(AppContext.BaseDirectory); while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Wasla.sln"))) folder = folder.Parent;
        var qa = Path.Combine(folder!.FullName, "_local-data", "doctor-patients-validation"); Directory.CreateDirectory(qa);
        await File.WriteAllTextAsync(Path.Combine(qa, "directory-query.sql"), capturedSql, TestContext.Current.CancellationToken);
        var evidence = $"SQL Server: 2,001 patients; 20,002 tickets after concurrent admission; 2 owned Practices; live/archive mix; page size 100; 21 distinct pages; first page {elapsed:F1} ms; exactly 5 reader queries; seed {seed.Elapsed.TotalSeconds:F1} s.";
        await File.WriteAllTextAsync(Path.Combine(qa, "performance.txt"), evidence, TestContext.Current.CancellationToken); output.WriteLine(evidence);
    }
    private sealed class QueryProbe : DbCommandInterceptor
    {
        public int Count { get; private set; }
        public string Sql { get; private set; } = "";
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Count++; Sql = command.CommandText;
            return ValueTask.FromResult(result);
        }
    }
}

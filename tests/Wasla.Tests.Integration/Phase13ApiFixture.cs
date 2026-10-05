using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Application.Time;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Patients;
using Wasla.Domain.Practices;
using Wasla.Domain.ReferenceData;
using Wasla.Domain.Security;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Tests.Integration;

internal sealed class Phase13ApiFixture : WebApplicationFactory<Program>
{
    private const string Password = "Phase13TestPass123!";
    private readonly SqliteConnection? _sqlite;
    private readonly string? _sqlConnection;
    public Guid PracticeId { get; } = Guid.NewGuid();
    public Guid OtherPracticeId { get; } = Guid.NewGuid();
    public Guid DoctorId { get; private set; }
    public Guid DoctorUserId { get; } = Guid.NewGuid();
    public Guid PatientId { get; } = Guid.NewGuid();
    public Guid OtherPatientId { get; } = Guid.NewGuid();
    public Guid SegmentId { get; } = Guid.NewGuid();
    public Guid ConsultationTypeId { get; } = Guid.NewGuid();
    public Guid FollowUpTypeId { get; } = Guid.NewGuid();
    public Guid ReceptionAssignmentId { get; } = Guid.NewGuid();
    public Phase13Clock Clock { get; } = new();
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(Clock.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo")));
    public string TicketUrl(Guid id) => $"/api/v1/practices/{PracticeId}/tickets/{id}";
    public string EncounterUrl(Guid id) => $"/api/v1/doctors/me/practices/{PracticeId}/encounters/{id}";

    private Phase13ApiFixture(string? sqlConnection)
    {
        if (sqlConnection is null) _sqlite = new SqliteConnection("Data Source=:memory:");
        else
        {
            var builder = new SqlConnectionStringBuilder(sqlConnection)
                { InitialCatalog = "WaslaPhase13Tests_" + Guid.NewGuid().ToString("N") };
            _sqlConnection = builder.ConnectionString;
        }
    }

    public static async Task<Phase13ApiFixture> CreateAsync(bool sqlServer = false)
    {
        var connection = sqlServer ? Environment.GetEnvironmentVariable("WASLA_SQLSERVER_CONNECTION_STRING") : null;
        if (sqlServer) Assert.SkipWhen(string.IsNullOrWhiteSpace(connection), "WASLA_SQLSERVER_CONNECTION_STRING is required.");
        var fixture = new Phase13ApiFixture(connection);
        if (fixture._sqlite is not null) await fixture._sqlite.OpenAsync(TestContext.Current.CancellationToken);
        using var client = fixture.CreateClient();
        try { await fixture.InitializeAsync(); }
        catch { await fixture.DisposeAsync(); throw; }
        return fixture;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = "phase13-test-signing-key-at-least-thirty-two-characters",
            ["Jwt:ExpirationMinutes"] = "60",
            ["Serilog:MinimumLevel:Default"] = "Warning",
            ["PasswordReset:HmacSecret"] = "phase13-test-hmac-secret-at-least-thirty-two-characters",
            ["EmailBranding:FooterImageUrl"] = "https://example.test/footer.png",
            ["DatabaseInitialization:ApplyMigrationsOnStartup"] = "false",
            ["DatabaseInitialization:ApplySeedingOnStartup"] = "false",
            ["EmailOutbox:Enabled"] = "false",
            ["RootSuperAdmin:UserName"] = "root", ["RootSuperAdmin:Email"] = "root@example.test",
            ["RootSuperAdmin:NameAr"] = "المشرف", ["RootSuperAdmin:Password"] = Password
        }));
        builder.ConfigureServices(services =>
        {
            // API scenarios drive lifecycle transitions explicitly and own database initialization.
            services.RemoveAll<IHostedService>();
            services.RemoveAll<DbContextOptions<WaslaDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<WaslaDbContext>>();
            services.RemoveAll<WaslaDbContext>();
            services.AddDbContext<WaslaDbContext>(options =>
            {
                if (_sqlite is not null) options.UseSqlite(_sqlite);
                else options.UseSqlServer(_sqlConnection);
            });
            services.RemoveAll<IDateTimeProvider>();
            services.AddSingleton<IDateTimeProvider>(Clock);
            // Authentication remains signed and permission-bearing; the business clock is controlled by each scenario.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,
                options => options.TokenValidationParameters.ValidateLifetime = false);
        });
    }

    private async Task InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
        if (_sqlConnection is not null) await db.Database.MigrateAsync(ct);
        else await db.Database.EnsureCreatedAsync(ct);
        var seederType = typeof(WaslaDbContext).Assembly.GetType("Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.WaslaSecuritySeeder")!;
        await (Task)seederType.GetMethod("SeedAsync")!.Invoke(scope.ServiceProvider.GetRequiredService(seederType), [ct])!;
        var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var hash = await passwordService.HashAsync(Password, ct);
        var now = Clock.UtcNow;
        var doctorUser = ApplicationUser.Create(DoctorUserId, "doctor", "doctor@example.test", null, hash, UserType.Doctor, false, now).Value;
        var otherDoctorUser = ApplicationUser.Create(Guid.NewGuid(), "other-doctor", "other-doctor@example.test", null, hash, UserType.Doctor, false, now).Value;
        var patientUser = ApplicationUser.Create(Guid.NewGuid(), "patient", "patient@example.test", null, hash, UserType.Patient, false, now).Value;
        var otherPatientUser = ApplicationUser.Create(Guid.NewGuid(), "other-patient", "other-patient@example.test", null, hash, UserType.Patient, false, now).Value;
        var receptionUser = ApplicationUser.Create(Guid.NewGuid(), "reception", "reception@example.test", null, hash, UserType.Reception, false, now).Value;
        db.ApplicationUsers.AddRange(doctorUser, otherDoctorUser, patientUser, otherPatientUser, receptionUser);
        foreach (var user in new[] { doctorUser, otherDoctorUser, patientUser, otherPatientUser, receptionUser })
        {
            var role = user.UserType switch { UserType.Doctor => SystemRoleIds.Doctor, UserType.Patient => SystemRoleIds.Patient, _ => SystemRoleIds.Reception };
            db.UserRoles.Add(new UserRole(Guid.NewGuid(), user.Id, role));
        }
        var doctor = Doctor.Create(Guid.NewGuid(), doctorUser.Id, "طبيب", "Doctor", new DateOnly(1980, 1, 1), Gender.Male, null, "front", "back", "card", null, Today).Value;
        doctor.Approve("29801011234567", doctorUser.Id, now); DoctorId = doctor.Id;
        var otherDoctor = Doctor.Create(Guid.NewGuid(), otherDoctorUser.Id, "طبيب آخر", "Other doctor", new DateOnly(1981, 1, 1), Gender.Male, null, "front", "back", "card", null, Today).Value;
        otherDoctor.Approve("29901011234567", otherDoctorUser.Id, now);
        db.Doctors.AddRange(doctor, otherDoctor);
        db.Governorates.Add(Governorate.Create(1, "القاهرة", "Cairo", 1).Value);
        db.Cities.Add(City.Create(1, 1, "القاهرة", "Cairo", 1).Value);
        db.Areas.Add(Area.Create(1, 1, "المنطقة", "Area", 1).Value);
        var practice = DoctorPractice.Create(PracticeId, doctor.Id, "عيادة", "Practice", 1, 1, 1, "Address", 30, 31, doctorUser.Id).Value;
        practice.Activate(true, true, true, doctorUser.Id);
        var otherPractice = DoctorPractice.Create(OtherPracticeId, otherDoctor.Id, "عيادة أخرى", "Other practice", 1, 1, 1, "Address", 30, 31, otherDoctorUser.Id).Value;
        otherPractice.Activate(true, true, true, otherDoctorUser.Id);
        db.DoctorPractices.AddRange(practice, otherPractice);
        db.DoctorPracticeConfigurations.AddRange(DoctorPracticeConfiguration.CreateDefault(Guid.NewGuid(), PracticeId, doctorUser.Id).Value,
            DoctorPracticeConfiguration.CreateDefault(Guid.NewGuid(), OtherPracticeId, otherDoctorUser.Id).Value);
        db.DoctorPracticeBrandings.Add(DoctorPracticeBranding.CreateDefault(Guid.NewGuid(), PracticeId, doctorUser.Id).Value);
        foreach (var day in Enum.GetValues<DayOfWeek>())
            db.DoctorPracticeSchedulePeriods.Add(DoctorPracticeSchedulePeriod.Create(Guid.NewGuid(), PracticeId, day, new TimeOnly(8, 0), new TimeOnly(23, 0), 20, doctorUser.Id).Value);
        db.Patients.AddRange(Patient.Create(PatientId, "مريض", "Patient", new DateOnly(1990, 1, 1), Gender.Male, null, null, null, null, null, Today).Value,
            Patient.Create(OtherPatientId, "مريض آخر", "Other patient", new DateOnly(1991, 1, 1), Gender.Male, null, null, null, null, null, Today).Value);
        db.PatientAccountLinks.AddRange(PatientAccountLink.Create(Guid.NewGuid(), patientUser.Id, PatientId, PatientAccountLinkSource.SelfRegistration, now).Value,
            PatientAccountLink.Create(Guid.NewGuid(), otherPatientUser.Id, OtherPatientId, PatientAccountLinkSource.SelfRegistration, now).Value);
        db.DoctorPracticeSegments.Add(DoctorPracticeSegment.CreateDefault(SegmentId, PracticeId, doctorUser.Id).Value);
        db.DoctorPracticeVisitTypes.AddRange(DoctorPracticeVisitType.CreateDefault(ConsultationTypeId, PracticeId, DoctorPracticeVisitTypeCode.NewConsultation, doctorUser.Id).Value,
            DoctorPracticeVisitType.CreateDefault(FollowUpTypeId, PracticeId, DoctorPracticeVisitTypeCode.FollowUp, doctorUser.Id).Value);
        db.DoctorPracticeSegmentVisitTypePrices.AddRange(DoctorPracticeSegmentVisitTypePrice.Create(Guid.NewGuid(), PracticeId, SegmentId, ConsultationTypeId, 300, doctorUser.Id).Value,
            DoctorPracticeSegmentVisitTypePrice.Create(Guid.NewGuid(), PracticeId, SegmentId, FollowUpTypeId, 100, doctorUser.Id).Value);
        var reception = Reception.Create(Guid.NewGuid(), receptionUser.Id, doctor.Id, "استقبال", "Reception", doctorUser.Id).Value;
        db.Receptions.Add(reception);
        db.ReceptionPracticeAssignments.Add(ReceptionPracticeAssignment.Create(ReceptionAssignmentId, reception.Id, PracticeId, doctorUser.Id).Value);
        db.ReceptionPracticeAssignmentPermissions.AddRange(PermissionNames.ReceptionAssignmentScoped.Select(permission =>
            new ReceptionPracticeAssignmentPermission(Guid.NewGuid(), ReceptionAssignmentId, SystemPermissionIds.For(permission), doctorUser.Id, now)));
        await db.SaveChangesAsync(ct);
    }

    public async Task<HttpClient> ClientAsync(string actor)
    {
        var client = CreateClient();
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = actor, password = Password }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var json = await JsonAsync(login);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("accessToken").GetString());
        return client;
    }

    public static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement.Clone();
    public static async Task<JsonElement> GetAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url, TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return await JsonAsync(response);
    }
    public static Task<HttpResponseMessage> PostAsync(HttpClient client, string url, object body, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("N"));
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }
    public async Task<JsonElement> WalkInAsync(HttpClient reception, Guid? eligibilityId = null, Guid? patientId = null)
    {
        using var response = await PostAsync(reception, $"/api/v1/practices/{PracticeId}/tickets/walk-in", new
        {
            patientId = patientId ?? PatientId, segmentId = SegmentId, visitTypeId = eligibilityId is null ? ConsultationTypeId : FollowUpTypeId,
            followUpEligibilityId = eligibilityId, paidAmount = eligibilityId is null ? 300 : 100, paymentMethod = "Cash"
        });
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return await JsonAsync(response);
    }
    public async Task<JsonElement> StartAsync(HttpClient doctor, JsonElement ticket)
    {
        var id = ticket.GetProperty("ticketId").GetGuid();
        using var call = await PostAsync(doctor, $"/api/v1/practices/{PracticeId}/queue/call-next", new { });
        Assert.Equal(HttpStatusCode.OK, call.StatusCode);
        ticket = await GetAsync(doctor, TicketUrl(id));
        using var start = await PostAsync(doctor, TicketUrl(id) + "/start", new { rowVersion = ticket.GetProperty("rowVersion").GetString() });
        Assert.True(start.StatusCode == HttpStatusCode.OK, await start.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return await JsonAsync(start);
    }
    public async Task<(JsonElement Ticket, JsonElement Encounter)> CompleteAsync(HttpClient doctor, JsonElement ticket)
    {
        var id = ticket.GetProperty("ticketId").GetGuid(); var encounterId = ticket.GetProperty("medicalEncounterId").GetGuid();
        var encounter = await GetAsync(doctor, EncounterUrl(encounterId));
        using var notes = await doctor.PatchAsJsonAsync(EncounterUrl(encounterId) + "/clinical-notes",
            new { clinicalNotes = "Clinical documentation", rowVersion = encounter.GetProperty("rowVersion").GetString() }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, notes.StatusCode); encounter = await JsonAsync(notes);
        using var complete = await PostAsync(doctor, TicketUrl(id) + "/complete", new
        {
            ticketRowVersion = ticket.GetProperty("rowVersion").GetString(), encounterRowVersion = encounter.GetProperty("rowVersion").GetString()
        });
        Assert.True(complete.StatusCode == HttpStatusCode.OK, await complete.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return (await JsonAsync(complete), await GetAsync(doctor, EncounterUrl(encounterId)));
    }
    public async Task<JsonElement> EligibilityAsync(HttpClient doctor, Guid encounterId, int days = 3)
    {
        using var response = await PostAsync(doctor, EncounterUrl(encounterId) + "/follow-up-eligibility", new { validUntil = Today.AddDays(days) });
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return await JsonAsync(response);
    }
    public async Task WithDbAsync(Func<WaslaDbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<WaslaDbContext>());
    }
    public override async ValueTask DisposeAsync()
    {
        if (_sqlConnection is not null)
        {
            var catalog = new SqlConnectionStringBuilder(_sqlConnection).InitialCatalog;
            if (!catalog.StartsWith("WaslaPhase13Tests_", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected test database.");
            await WithDbAsync(db => db.Database.EnsureDeletedAsync());
        }
        await base.DisposeAsync();
        if (_sqlite is not null) await _sqlite.DisposeAsync();
    }
}

internal sealed class Phase13Clock : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = new(2026, 10, 4, 9, 0, 0, DateTimeKind.Utc);
}

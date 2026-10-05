using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlock.Application.Abstraction.Encryption;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Wasla.Application.Features.Tickets.GetWalkInOptions;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Patients;
using Wasla.Domain.Payments;
using Wasla.Domain.Practices;
using Wasla.Domain.Security;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Tests.Integration;

public sealed class ReceptionWalkInOptionsTests
{
    [Fact]
    public async Task Authorized_reception_gets_priced_options_without_record_payment_or_reservation_slots()
    {
        await using var factory = await WalkInApiFactory.CreateAsync();
        using var client = await factory.CreateReceptionClientAsync();
        using var response = await client.GetAsync(factory.OptionsUrl, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var options = await response.Content.ReadFromJsonAsync<WalkInOptionsResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(options);
        Assert.Equal(factory.PracticeId, options.PracticeId);
        Assert.Equal(FinancialPolicy.CurrencyCode, options.CurrencyCode);
        var segment = Assert.Single(options.Segments);
        Assert.Equal(factory.SegmentId, segment.SegmentId);
        Assert.Equal("عادي", segment.NameAr);
        Assert.Equal("Normal", segment.NameEn);
        Assert.Equal(0, segment.Priority);
        var visitType = Assert.Single(segment.VisitTypes);
        Assert.Equal(factory.VisitTypeId, visitType.VisitTypeId);
        Assert.Equal("NewConsultation", visitType.Code);
        Assert.Equal(500m, visitType.Price);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
        var receptionPermissions = await (from mapping in db.RolePermissions
            join permission in db.Permissions on mapping.PermissionId equals permission.Id
            where mapping.RoleId == SystemRoleIds.Reception
            select permission.Name).ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.All(new[]
        {
            PermissionNames.PracticeReservationsView, PermissionNames.PracticeTicketsView,
            PermissionNames.PracticeTicketsCall, PermissionNames.PracticeTicketsManualCall,
            PermissionNames.PracticeTicketsRestoreNoShow, PermissionNames.PracticeTicketsCancel,
            PermissionNames.PracticeTicketsCreateWalkIn, PermissionNames.PracticeTicketsRecordPayment,
            PermissionNames.PracticePaymentsView, PermissionNames.PracticePaymentsCorrect,
            PermissionNames.PracticePaymentsRefund
        }, permission => Assert.Contains(permission, receptionPermissions));
        Assert.DoesNotContain(PermissionNames.DoctorPracticeSegmentsViewOwn, receptionPermissions);
        Assert.DoesNotContain(PermissionNames.DoctorPracticePricingViewOwn, receptionPermissions);
        Assert.Empty(await db.DoctorPracticeSchedulePeriods.ToArrayAsync(
            TestContext.Current.CancellationToken));
        Assert.Single(await db.ReceptionPracticeAssignmentPermissions.ToArrayAsync(
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("assignment")]
    [InlineData("role")]
    public async Task Missing_create_walk_in_permission_is_forbidden(string missingPermission)
    {
        await using var factory = await WalkInApiFactory.CreateAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
            if (missingPermission == "assignment")
            {
                db.ReceptionPracticeAssignmentPermissions.RemoveRange(
                    db.ReceptionPracticeAssignmentPermissions);
            }
            else
            {
                db.RolePermissions.RemoveRange(db.RolePermissions.Where(item =>
                    item.RoleId == SystemRoleIds.Reception &&
                    item.PermissionId == SystemPermissionIds.For(PermissionNames.PracticeTicketsCreateWalkIn)));
            }

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var client = await factory.CreateReceptionClientAsync();
        using var response = await client.GetAsync(factory.OptionsUrl, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Disabled_walk_in_returns_existing_error()
    {
        await using var factory = await WalkInApiFactory.CreateAsync("disabled");
        using var client = await factory.CreateReceptionClientAsync();
        using var response = await client.GetAsync(factory.OptionsUrl, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Ticket.WalkInNotAllowed",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("inactive-segment")]
    [InlineData("inactive-visit-type")]
    [InlineData("follow-up")]
    [InlineData("no-price")]
    [InlineData("other-practice")]
    public async Task Invalid_catalog_combinations_are_excluded_and_empty_catalog_succeeds(string catalogState)
    {
        await using var factory = await WalkInApiFactory.CreateAsync(catalogState);
        using var client = await factory.CreateReceptionClientAsync();
        using var response = await client.GetAsync(factory.OptionsUrl, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var options = await response.Content.ReadFromJsonAsync<WalkInOptionsResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(options);
        Assert.Equal(factory.PracticeId, options.PracticeId);
        Assert.Equal("EGP", options.CurrencyCode);
        Assert.Empty(options.Segments);
    }

    [Fact]
    public async Task Returned_option_creates_ticket_and_full_payment_and_existing_routes_keep_their_permissions()
    {
        await using var factory = await WalkInApiFactory.CreateAsync();
        using var client = await factory.CreateReceptionClientAsync();
        var token = TestContext.Current.CancellationToken;
        var options = await client.GetFromJsonAsync<WalkInOptionsResponse>(factory.OptionsUrl, token);
        var segment = Assert.Single(options!.Segments);
        var visitType = Assert.Single(segment.VisitTypes);

        foreach (var route in new[]
        {
            $"/api/v1/practices/{factory.PracticeId}/queue",
            $"/api/v1/reception/practices/{factory.PracticeId}/reservations/filter-options",
            $"/api/v1/practices/{factory.PracticeId}/financial-transactions"
        })
        {
            using var denied = await client.GetAsync(route, token);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }

        foreach (var catalog in new[] { "segments", "visit-types", "prices" })
        {
            using var denied = await client.GetAsync(
                $"/api/v1/doctors/me/practices/{factory.PracticeId}/{catalog}", token);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }

        var admissionUrl = $"/api/v1/practices/{factory.PracticeId}/tickets/walk-in";
        var admission = new
        {
            factory.PatientId, segment.SegmentId, visitType.VisitTypeId,
            paidAmount = visitType.Price, paymentMethod = "Cash"
        };
        client.DefaultRequestHeaders.Add("Idempotency-Key", "walk-in-options-integration");
        using var paymentDenied = await client.PostAsJsonAsync(admissionUrl, admission, token);
        Assert.Equal(HttpStatusCode.Forbidden, paymentDenied.StatusCode);

        await factory.DelegateAsync(PermissionNames.PracticeTicketsRecordPayment,
            PermissionNames.PracticeTicketsView, PermissionNames.PracticeReservationsView,
            PermissionNames.PracticePaymentsView);
        using var created = await client.PostAsJsonAsync(admissionUrl, admission, token);
        Assert.True(created.StatusCode == HttpStatusCode.Created,
            await created.Content.ReadAsStringAsync(token));

        foreach (var route in new[]
        {
            $"/api/v1/practices/{factory.PracticeId}/queue",
            $"/api/v1/reception/practices/{factory.PracticeId}/reservations/filter-options",
            $"/api/v1/practices/{factory.PracticeId}/financial-transactions"
        })
        {
            using var allowed = await client.GetAsync(route, token);
            Assert.True(allowed.StatusCode == HttpStatusCode.OK,
                await allowed.Content.ReadAsStringAsync(token));
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
        var ticket = await db.Tickets.SingleAsync(token);
        var payment = await db.Payments.SingleAsync(token);
        Assert.Equal(ticket.Id, payment.TicketId);
        Assert.Equal(visitType.Price, ticket.PriceSnapshot);
        Assert.Equal(visitType.Price, payment.Amount);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Equal("EGP", payment.CurrencyCode);
    }

    private sealed class WalkInApiFactory : WebApplicationFactory<Program>
    {
        private const string Password = "ReceptionTest123!";
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly Guid _receptionUserId = Guid.NewGuid();
        private readonly Guid _assignmentId = Guid.NewGuid();

        public Guid PracticeId { get; } = Guid.NewGuid();
        public Guid SegmentId { get; } = Guid.NewGuid();
        public Guid VisitTypeId { get; } = Guid.NewGuid();
        public Guid PatientId { get; } = Guid.NewGuid();
        public string OptionsUrl => $"/api/v1/reception/practices/{PracticeId}/walk-in/options";

        public static async Task<WalkInApiFactory> CreateAsync(string catalogState = "valid")
        {
            var factory = new WalkInApiFactory();
            await factory._connection.OpenAsync(TestContext.Current.CancellationToken);
            using var client = factory.CreateClient();
            await factory.SeedAsync(catalogState);
            return factory;
        }

        public async Task<HttpClient> CreateReceptionClientAsync()
        {
            var client = CreateClient();
            using var login = await client.PostAsJsonAsync("/api/v1/auth/login",
                new { identifier = "walk-in-reception", password = Password },
                TestContext.Current.CancellationToken);
            Assert.True(login.StatusCode == HttpStatusCode.OK,
                await login.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var response = await login.Content.ReadFromJsonAsync<JsonElement>(
                TestContext.Current.CancellationToken);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", response.GetProperty("accessToken").GetString());
            return client;
        }

        public async Task DelegateAsync(params string[] permissions)
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
            db.ReceptionPracticeAssignmentPermissions.AddRange(permissions.Select(permission =>
                new ReceptionPracticeAssignmentPermission(Guid.NewGuid(), _assignmentId,
                    SystemPermissionIds.For(permission), _receptionUserId, DateTime.UtcNow)));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:SigningKey"] = "test-jwt-signing-key-with-at-least-thirty-two-characters",
                    ["PasswordReset:HmacSecret"] = "test-password-reset-hmac-secret-at-least-32-chars",
                    ["EmailBranding:FooterImageUrl"] = "https://api.example.test/email-assets/wasla-email-footer.png",
                    ["DatabaseInitialization:ApplyMigrationsOnStartup"] = "false",
                    ["DatabaseInitialization:ApplySeedingOnStartup"] = "false",
                    ["EmailOutbox:Enabled"] = "false",
                    ["RootSuperAdmin:UserName"] = "root",
                    ["RootSuperAdmin:Email"] = "root@example.test",
                    ["RootSuperAdmin:NameAr"] = "المشرف الجذر",
                    ["RootSuperAdmin:Password"] = "RootAdminPass123!"
                }));
            builder.ConfigureServices(services =>
            {
                // This fixture owns initialization and has no background-processing scenarios.
                services.RemoveAll<IHostedService>();
                services.RemoveAll<DbContextOptions<WaslaDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<WaslaDbContext>>();
                services.RemoveAll<WaslaDbContext>();
                services.AddDbContext<WaslaDbContext>(options => options.UseSqlite(_connection));
            });
        }

        private async Task SeedAsync(string catalogState)
        {
            var token = TestContext.Current.CancellationToken;
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
            await db.Database.EnsureCreatedAsync(token);
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF;", token);
            var seederType = typeof(WaslaDbContext).Assembly.GetType(
                "Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.WaslaSecuritySeeder")!;
            await (Task)seederType.GetMethod("SeedAsync")!.Invoke(
                scope.ServiceProvider.GetRequiredService(seederType), [token])!;

            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);
            var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
            var doctorUser = ApplicationUser.Create(Guid.NewGuid(), "walk-in-doctor",
                "doctor@example.test", null, "hash", UserType.Doctor, false, now).Value;
            var receptionUser = ApplicationUser.Create(_receptionUserId, "walk-in-reception",
                "reception@example.test", null, await passwordService.HashAsync(Password, token),
                UserType.Reception, false, now).Value;
            var doctor = Doctor.Create(Guid.NewGuid(), doctorUser.Id, "طبيب", "Doctor",
                new DateOnly(1980, 1, 1), Gender.Male, null, "front", "back", "card", null, today).Value;
            doctor.Approve("123", doctorUser.Id, now);
            var practice = DoctorPractice.Create(PracticeId, doctor.Id, "عيادة", "Practice",
                1, 1, 1, "Address", 30, 31, doctorUser.Id).Value;
            practice.Activate(true, true, true, doctorUser.Id);
            var configuration = DoctorPracticeConfiguration.CreateDefault(
                Guid.NewGuid(), PracticeId, doctorUser.Id).Value;
            if (catalogState == "disabled")
            {
                Assert.True(configuration.Update(configuration.AllowOnlineBooking, false,
                    configuration.DefaultSlotDurationMinutes, configuration.CheckInGracePeriodMinutes,
                    configuration.PatientSelfCancellationCutoffMinutes, configuration.MaximumDailyPatients,
                    configuration.MaximumTicketCallAttempts, configuration.TimeZoneId, doctorUser.Id).IsSuccess);
            }

            var catalogPracticeId = catalogState == "other-practice" ? Guid.NewGuid() : PracticeId;
            var segment = DoctorPracticeSegment.Create(SegmentId, catalogPracticeId,
                "عادي", "Normal", 0, null, null, actorId: doctorUser.Id).Value;
            if (catalogState == "inactive-segment")
            {
                Assert.True(segment.Deactivate(doctorUser.Id).IsSuccess);
            }

            var visitType = DoctorPracticeVisitType.CreateDefault(VisitTypeId, catalogPracticeId,
                catalogState == "follow-up" ? DoctorPracticeVisitTypeCode.FollowUp :
                    DoctorPracticeVisitTypeCode.NewConsultation, doctorUser.Id).Value;
            if (catalogState == "inactive-visit-type")
            {
                Assert.True(visitType.Update(visitType.NameAr, visitType.NameEn, false, doctorUser.Id).IsSuccess);
            }

            var reception = Reception.Create(Guid.NewGuid(), _receptionUserId, doctor.Id,
                "استقبال", "Reception", doctorUser.Id).Value;
            db.ApplicationUsers.AddRange(doctorUser, receptionUser);
            db.UserRoles.Add(new UserRole(Guid.NewGuid(), _receptionUserId, SystemRoleIds.Reception));
            db.Doctors.Add(doctor);
            db.DoctorPractices.Add(practice);
            db.DoctorPracticeConfigurations.Add(configuration);
            db.DoctorPracticeSegments.Add(segment);
            db.DoctorPracticeVisitTypes.Add(visitType);
            if (catalogState != "no-price")
            {
                db.DoctorPracticeSegmentVisitTypePrices.Add(DoctorPracticeSegmentVisitTypePrice.Create(
                    Guid.NewGuid(), catalogPracticeId, SegmentId, VisitTypeId, 500m, doctorUser.Id).Value);
            }

            db.Receptions.Add(reception);
            db.ReceptionPracticeAssignments.Add(ReceptionPracticeAssignment.Create(
                _assignmentId, reception.Id, PracticeId, doctorUser.Id).Value);
            db.Patients.Add(Patient.Create(PatientId, "مريض", "Patient", new DateOnly(1990, 1, 1),
                Gender.Male, null, null, null, null, null, today).Value);
            await db.SaveChangesAsync(token);
            await DelegateAsync(PermissionNames.PracticeTicketsCreateWalkIn);
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}

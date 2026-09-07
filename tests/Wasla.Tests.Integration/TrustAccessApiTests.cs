using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BuildingBlock.Application.Abstraction.Encryption;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wasla.Domain.Common;
using Wasla.Domain.Security;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Tests.Integration;

public sealed class TrustAccessApiTests
{
    private const string RootPassword = "RootPass123";

    [Fact]
    public async Task DoctorPatientAndRootGovernance_FlowEndToEnd()
    {
        await using var factory = await WaslaApiFactory.CreateAsync();
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var testToken = TestContext.Current.CancellationToken;

        var protectedResponse = await anonymous.GetAsync("/api/v1/admin/doctors", testToken);
        Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);

        using var doctorRegistration = CreateDoctorRegistration("doctor1", "doctor1@example.test");
        var registered = await anonymous.PostAsync("/api/v1/auth/doctors/register", doctorRegistration, testToken);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var registrationJson = await ReadJsonAsync(registered, testToken);
        var doctorId = registrationJson.GetProperty("doctorId").GetGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
            var storedDoctor = await db.Doctors.AsNoTracking().SingleAsync(item => item.Id == doctorId, testToken);
            Assert.StartsWith($"Doctors/{doctorId}/", storedDoctor.PersonalIdFrontMediaKey, StringComparison.Ordinal);
            Assert.False(Path.IsPathRooted(storedDoctor.PersonalIdFrontMediaKey));
        }

        var privateStaticAttempt = await anonymous.GetAsync(
            $"/App_Data/Media/Doctors/{doctorId}/Verification/PersonalIdentity/Front/file.png",
            testToken);
        Assert.Equal(HttpStatusCode.NotFound, privateStaticAttempt.StatusCode);

        var doctorToken = await LoginAsync(anonymous, "doctor1", "DoctorPass123", testToken);
        using var doctorClient = factory.CreateClient();
        doctorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", doctorToken);
        var me = await doctorClient.GetAsync("/api/v1/auth/me", testToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var meJson = await ReadJsonAsync(me, testToken);
        Assert.Equal("Doctor", meJson.GetProperty("userType").GetString());
        Assert.Equal(doctorId, meJson.GetProperty("doctorId").GetGuid());

        var onboarding = await doctorClient.GetAsync("/api/v1/doctors/me/onboarding", testToken);
        Assert.Equal(HttpStatusCode.OK, onboarding.StatusCode);
        var onboardingJson = await ReadJsonAsync(onboarding, testToken);
        Assert.Equal("Pending", onboardingJson.GetProperty("approvalStatus").GetString());
        var rowVersion = onboardingJson.GetProperty("rowVersion").GetString()!;

        var rootToken = await LoginAsync(anonymous, "root", RootPassword, testToken);
        using var rootClient = factory.CreateClient();
        rootClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", rootToken);
        var doctorList = await rootClient.GetAsync("/api/v1/admin/doctors?approvalStatus=Pending", testToken);
        Assert.Equal(HttpStatusCode.OK, doctorList.StatusCode);
        var listJson = await ReadJsonAsync(doctorList, testToken);
        Assert.Equal(1, listJson.GetProperty("totalCount").GetInt64());

        var details = await rootClient.GetAsync($"/api/v1/admin/doctors/{doctorId}", testToken);
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        var media = await rootClient.GetAsync(
            $"/api/v1/admin/doctors/{doctorId}/media/PersonalIdFront",
            testToken);
        Assert.Equal(HttpStatusCode.OK, media.StatusCode);
        Assert.Equal("image/png", media.Content.Headers.ContentType!.MediaType);

        var approved = await rootClient.PostAsJsonAsync(
            $"/api/v1/admin/doctors/{doctorId}/approve",
            new { nationalId = "N-100", rowVersion },
            testToken);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var approvedJson = await ReadJsonAsync(approved, testToken);
        Assert.Equal("Approved", approvedJson.GetProperty("approvalStatus").GetString());
        var staleSuspend = await rootClient.PostAsJsonAsync(
            $"/api/v1/admin/doctors/{doctorId}/suspend",
            new { reason = "stale", rowVersion },
            testToken);
        Assert.Equal(HttpStatusCode.Conflict, staleSuspend.StatusCode);

        var approvedDetails = await rootClient.GetAsync($"/api/v1/admin/doctors/{doctorId}", testToken);
        var approvedRowVersion = (await ReadJsonAsync(approvedDetails, testToken))
            .GetProperty("rowVersion").GetString()!;
        var suspended = await rootClient.PostAsJsonAsync(
            $"/api/v1/admin/doctors/{doctorId}/suspend",
            new { reason = "governance review", rowVersion = approvedRowVersion },
            testToken);
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);
        var suspendedJson = await ReadJsonAsync(suspended, testToken);
        Assert.Equal("Suspended", suspendedJson.GetProperty("approvalStatus").GetString());
        var suspendedOnboarding = await doctorClient.GetAsync("/api/v1/doctors/me/onboarding", testToken);
        Assert.Equal(HttpStatusCode.OK, suspendedOnboarding.StatusCode);
        Assert.Equal(
            "Suspended",
            (await ReadJsonAsync(suspendedOnboarding, testToken)).GetProperty("approvalStatus").GetString());
        var reactivated = await rootClient.PostAsJsonAsync(
            $"/api/v1/admin/doctors/{doctorId}/reactivate",
            new { rowVersion = suspendedJson.GetProperty("rowVersion").GetString() },
            testToken);
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
        Assert.Equal(
            "Approved",
            (await ReadJsonAsync(reactivated, testToken)).GetProperty("approvalStatus").GetString());

        using (var rejectedRegistration = CreateDoctorRegistration("doctor2", "doctor2@example.test"))
        {
            Assert.Equal(
                HttpStatusCode.Created,
                (await anonymous.PostAsync("/api/v1/auth/doctors/register", rejectedRegistration, testToken)).StatusCode);
        }

        var doctor2Token = await LoginAsync(anonymous, "doctor2", "DoctorPass123", testToken);
        using var doctor2Client = factory.CreateClient();
        doctor2Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", doctor2Token);
        var doctor2Onboarding = await doctor2Client.GetAsync("/api/v1/doctors/me/onboarding", testToken);
        var doctor2OnboardingJson = await ReadJsonAsync(doctor2Onboarding, testToken);
        var doctor2Id = doctor2OnboardingJson.GetProperty("doctorId").GetGuid();
        var rejected = await rootClient.PostAsJsonAsync(
            $"/api/v1/admin/doctors/{doctor2Id}/reject",
            new
            {
                reason = "verification mismatch",
                rowVersion = doctor2OnboardingJson.GetProperty("rowVersion").GetString()
            },
            testToken);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        var rejectedOnboarding = await doctor2Client.GetAsync("/api/v1/doctors/me/onboarding", testToken);
        var rejectedOnboardingJson = await ReadJsonAsync(rejectedOnboarding, testToken);
        Assert.Equal("Rejected", rejectedOnboardingJson.GetProperty("approvalStatus").GetString());
        Assert.Equal("verification mismatch", rejectedOnboardingJson.GetProperty("rejectionReason").GetString());

        using var patientRegistration = CreatePatientRegistration("patient1", "patient1@example.test");
        var patientRegistered = await anonymous.PostAsync("/api/v1/auth/patients/register", patientRegistration, testToken);
        Assert.Equal(HttpStatusCode.Created, patientRegistered.StatusCode);
        using (var duplicateUserName = CreatePatientRegistration("patient1", "another@example.test"))
        {
            Assert.Equal(
                HttpStatusCode.Conflict,
                (await anonymous.PostAsync("/api/v1/auth/patients/register", duplicateUserName, testToken)).StatusCode);
        }

        using (var duplicateEmail = CreatePatientRegistration("another-patient", "patient1@example.test"))
        {
            Assert.Equal(
                HttpStatusCode.Conflict,
                (await anonymous.PostAsync("/api/v1/auth/patients/register", duplicateEmail, testToken)).StatusCode);
        }

        var patientToken = await LoginAsync(anonymous, "patient1@example.test", "PatientPass123", testToken);
        using var patientClient = factory.CreateClient();
        patientClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", patientToken);
        var patientMe = await patientClient.GetAsync("/api/v1/auth/me", testToken);
        Assert.Equal(HttpStatusCode.OK, patientMe.StatusCode);
        var patientJson = await ReadJsonAsync(patientMe, testToken);
        Assert.Equal("Patient", patientJson.GetProperty("userType").GetString());
        Assert.Equal("Patient", patientJson.GetProperty("roles")[0].GetString());

        var createdAdmin = await rootClient.PostAsJsonAsync(
            "/api/v1/admin/superadmins",
            new
            {
                userName = "admin1",
                email = "admin1@example.test",
                phoneNumber = "0101",
                nameAr = "مشرف",
                nameEn = "Admin",
                initialPassword = "AdminPass123",
                confirmPassword = "AdminPass123"
            },
            testToken);
        Assert.Equal(HttpStatusCode.Created, createdAdmin.StatusCode);
        var createdAdminJson = await ReadJsonAsync(createdAdmin, testToken);
        var createdAdminId = createdAdminJson.GetProperty("superAdminId").GetGuid();
        var adminToken = await LoginAsync(anonymous, "admin1", "AdminPass123", testToken);
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var changedPassword = await adminClient.PostAsJsonAsync(
            "/api/v1/auth/change-password",
            new
            {
                currentPassword = "AdminPass123",
                newPassword = "AdminPass456",
                confirmPassword = "AdminPass456"
            },
            testToken);
        Assert.Equal(HttpStatusCode.NoContent, changedPassword.StatusCode);
        adminToken = await LoginAsync(anonymous, "admin1", "AdminPass456", testToken);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var forbidden = await adminClient.GetAsync("/api/v1/admin/superadmins", testToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var adminRoleChange = await adminClient.PutAsJsonAsync(
            $"/api/v1/admin/roles/{SystemRoleIds.SuperAdmin}/permissions",
            new { permissionIds = Array.Empty<Guid>() },
            testToken);
        Assert.Equal(HttpStatusCode.Forbidden, adminRoleChange.StatusCode);

        var rootPermissionAssignment = await rootClient.PutAsJsonAsync(
            $"/api/v1/admin/roles/{SystemRoleIds.Doctor}/permissions",
            new
            {
                permissionIds = new[]
                {
                    SystemPermissionIds.For(PermissionNames.SuperAdminsViewAll)
                }
            },
            testToken);
        Assert.Equal(HttpStatusCode.Forbidden, rootPermissionAssignment.StatusCode);

        var admins = await rootClient.GetAsync("/api/v1/admin/superadmins", testToken);
        var adminsJson = await ReadJsonAsync(admins, testToken);
        var rootAdminId = adminsJson.GetProperty("items")
            .EnumerateArray()
            .Single(item => item.GetProperty("isRootSuperAdmin").GetBoolean())
            .GetProperty("superAdminId")
            .GetGuid();
        var rootDelete = await rootClient.DeleteAsync(
            $"/api/v1/admin/superadmins/{rootAdminId}",
            testToken);
        Assert.Equal(HttpStatusCode.Conflict, rootDelete.StatusCode);

        var deleted = await rootClient.DeleteAsync($"/api/v1/admin/superadmins/{createdAdminId}", testToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var inactiveLogin = await anonymous.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { identifier = "admin1", password = "AdminPass456" },
            testToken);
        Assert.Equal(HttpStatusCode.Unauthorized, inactiveLogin.StatusCode);
        var restored = await rootClient.PostAsync(
            $"/api/v1/admin/superadmins/{createdAdminId}/restore",
            null,
            testToken);
        Assert.Equal(HttpStatusCode.NoContent, restored.StatusCode);
        _ = await LoginAsync(anonymous, "admin1", "AdminPass456", testToken);
    }

    [Fact]
    public async Task PasswordRecovery_HashesProofsAndConsumesChallengeOnce()
    {
        await using var factory = await WaslaApiFactory.CreateAsync();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        using var registration = CreatePatientRegistration("recover", "recover@example.test");
        Assert.Equal(
            HttpStatusCode.Created,
            (await client.PostAsync("/api/v1/auth/patients/register", registration, testToken)).StatusCode);

        var request = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password/request-otp",
            new { email = "recover@example.test" },
            testToken);
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);
        var requestId = (await ReadJsonAsync(request, testToken)).GetProperty("requestId").GetGuid();

        string otp;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
            var challenge = await db.PasswordResetChallenges.SingleAsync(
                item => item.Id == requestId,
                testToken);
            var outbox = await db.EmailOutboxMessages.SingleAsync(
                item => item.IdempotencyKey == $"password-reset-otp:{requestId}",
                testToken);
            otp = Regex.Match(outbox.HtmlBody, @"\b\d{6}\b", RegexOptions.CultureInvariant).Value;
            Assert.NotEmpty(otp);
            Assert.DoesNotContain(otp, challenge.OtpHash, StringComparison.Ordinal);
        }

        var verify = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password/verify-otp",
            new { requestId, otp },
            testToken);
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var resetToken = (await ReadJsonAsync(verify, testToken)).GetProperty("resetToken").GetString()!;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
            var challenge = await db.PasswordResetChallenges.AsNoTracking().SingleAsync(testToken);
            Assert.NotEqual(resetToken, challenge.ResetTokenHash);
        }

        var reset = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password/reset",
            new
            {
                requestId,
                resetToken,
                newPassword = "RecoveredPass123",
                confirmPassword = "RecoveredPass123"
            },
            testToken);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        _ = await LoginAsync(client, "recover", "RecoveredPass123", testToken);

        var reused = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password/reset",
            new
            {
                requestId,
                resetToken,
                newPassword = "AnotherPass123",
                confirmPassword = "AnotherPass123"
            },
            testToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused.StatusCode);
    }

    [Fact]
    public async Task AcceptLanguage_LocalizesValidationButKeepsMachineCodesStable()
    {
        await using var factory = await WaslaApiFactory.CreateAsync();
        using var client = factory.CreateClient();
        var rootToken = await LoginAsync(
            client,
            "root",
            RootPassword,
            TestContext.Current.CancellationToken);

        var english = await SendInvalidCreateAsync(client, rootToken, "en");
        var arabic = await SendInvalidCreateAsync(client, rootToken, "ar");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, english.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, arabic.StatusCode);
        var englishJson = await ReadJsonAsync(english, TestContext.Current.CancellationToken);
        var arabicJson = await ReadJsonAsync(arabic, TestContext.Current.CancellationToken);
        var englishError = englishJson.GetProperty("errors")[0];
        var arabicError = arabicJson.GetProperty("errors")[0];
        Assert.Equal(englishError.GetProperty("code").GetString(), arabicError.GetProperty("code").GetString());
        Assert.NotEqual(englishError.GetProperty("message").GetString(), arabicError.GetProperty("message").GetString());
        Assert.True(englishJson.TryGetProperty("traceId", out _));
        Assert.True(arabicJson.TryGetProperty("correlationId", out _));

        using var englishAuthRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/doctors");
        englishAuthRequest.Headers.AcceptLanguage.ParseAdd("en");
        using var arabicAuthRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/doctors");
        arabicAuthRequest.Headers.AcceptLanguage.ParseAdd("ar");
        var englishAuth = await client.SendAsync(englishAuthRequest, TestContext.Current.CancellationToken);
        var arabicAuth = await client.SendAsync(arabicAuthRequest, TestContext.Current.CancellationToken);
        var englishAuthJson = await ReadJsonAsync(englishAuth, TestContext.Current.CancellationToken);
        var arabicAuthJson = await ReadJsonAsync(arabicAuth, TestContext.Current.CancellationToken);
        Assert.Equal(
            englishAuthJson.GetProperty("errors")[0].GetProperty("code").GetString(),
            arabicAuthJson.GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.NotEqual(
            englishAuthJson.GetProperty("errors")[0].GetProperty("message").GetString(),
            arabicAuthJson.GetProperty("errors")[0].GetProperty("message").GetString());
    }

    [Fact]
    public async Task DoctorRegistration_ValidatesDocumentsAndCleansMediaWhenPersistenceFails()
    {
        await using var factory = await WaslaApiFactory.CreateAsync();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        using (var missingDocuments = BaseRegistration(
                   "missing-docs",
                   "missing-docs@example.test",
                   "DoctorPass123"))
        {
            var invalid = await client.PostAsync(
                "/api/v1/auth/doctors/register",
                missingDocuments,
                testToken);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER fail_doctor_insert BEFORE INSERT ON Doctors BEGIN SELECT RAISE(FAIL, 'forced-doctor-failure'); END;",
                testToken);
        }

        using (var failingRegistration = CreateDoctorRegistration("failure", "failure@example.test"))
        {
            var failed = await client.PostAsync(
                "/api/v1/auth/doctors/register",
                failingRegistration,
                testToken);
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        }

        Assert.True(Directory.Exists(factory.MediaRoot));
        Assert.Empty(Directory.EnumerateFiles(factory.MediaRoot, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task SecuritySeeder_IsOrderedDeterministicAndIdempotent()
    {
        await using var factory = await WaslaApiFactory.CreateAsync(seedSecurityData: false);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
        var seederType = typeof(WaslaDbContext).Assembly.GetType(
            "Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.WaslaSecuritySeeder",
            throwOnError: true)!;
        var seeder = scope.ServiceProvider.GetRequiredService(seederType);
        var seedMethod = seederType.GetMethod("SeedAsync")!;

        await (Task)seedMethod.Invoke(seeder, [TestContext.Current.CancellationToken])!;
        await (Task)seedMethod.Invoke(seeder, [TestContext.Current.CancellationToken])!;
        db.ChangeTracker.Clear();

        Assert.Equal(4, await db.Roles.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(PermissionNames.All.Count, await db.Permissions.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.SuperAdmins.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.SuperAdmins.CountAsync(
            admin => admin.IsRootSuperAdmin,
            TestContext.Current.CancellationToken));
        Assert.DoesNotContain(
            await db.RolePermissions
                .Where(mapping => mapping.RoleId == SystemRoleIds.SuperAdmin)
                .Select(mapping => mapping.PermissionId)
                .ToListAsync(TestContext.Current.CancellationToken),
            permissionId => PermissionNames.RootOnly.Any(name => SystemPermissionIds.For(name) == permissionId));
    }

    private static MultipartFormDataContent CreateDoctorRegistration(string userName, string email)
    {
        var content = BaseRegistration(userName, email, "DoctorPass123");
        AddFile(content, "PersonalIdFrontImage", "front.png");
        AddFile(content, "PersonalIdBackImage", "back.png");
        AddFile(content, "SyndicateCardFrontImage", "syndicate.png");
        return content;
    }

    private static MultipartFormDataContent CreatePatientRegistration(string userName, string email)
        => BaseRegistration(userName, email, "PatientPass123");

    private static MultipartFormDataContent BaseRegistration(string userName, string email, string password)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(userName), "UserName");
        content.Add(new StringContent(email), "Email");
        content.Add(new StringContent("01000000000"), "PhoneNumber");
        content.Add(new StringContent(password), "Password");
        content.Add(new StringContent(password), "ConfirmPassword");
        content.Add(new StringContent("اسم عربي"), "NameAr");
        content.Add(new StringContent("English Name"), "NameEn");
        content.Add(new StringContent("1991-04-12"), "DateOfBirth");
        content.Add(new StringContent("Male"), "Gender");
        return content;
    }

    private static void AddFile(MultipartFormDataContent content, string fieldName, string fileName)
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 };
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, fieldName, fileName);
    }

    private static async Task<string> LoginAsync(
        HttpClient client,
        string identifier,
        string password,
        CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { identifier, password },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadJsonAsync(response, cancellationToken)).GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> SendInvalidCreateAsync(
        HttpClient client,
        string token,
        string language)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/superadmins")
        {
            Content = new StringContent(
                "{\"userName\":\"\",\"email\":\"\",\"phoneNumber\":null,\"nameAr\":\"\",\"nameEn\":null,\"initialPassword\":\"\",\"confirmPassword\":\"\"}",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.AcceptLanguage.ParseAdd(language);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<JsonElement> ReadJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
        => (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).Clone();

    private sealed class WaslaApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly string _mediaRoot = Path.Combine(Path.GetTempPath(), $"wasla-api-tests-{Guid.NewGuid():N}");

        public string MediaRoot => _mediaRoot;

        public static async Task<WaslaApiFactory> CreateAsync(bool seedSecurityData = true)
        {
            var factory = new WaslaApiFactory();
            await factory._connection.OpenAsync();
            _ = factory.CreateClient();
            await factory.InitializeDatabaseAsync(seedSecurityData);
            return factory;
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
                    ["MediaStorage:RootPath"] = _mediaRoot,
                    ["DatabaseInitialization:ApplyMigrationsOnStartup"] = "false",
                    ["DatabaseInitialization:ApplySeedingOnStartup"] = "false",
                    ["EmailOutbox:Enabled"] = "false",
                    ["RootSuperAdmin:UserName"] = "root",
                    ["RootSuperAdmin:Email"] = "root@example.test",
                    ["RootSuperAdmin:NameAr"] = "المشرف الجذر",
                    ["RootSuperAdmin:NameEn"] = "Root",
                    ["RootSuperAdmin:Password"] = RootPassword
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<WaslaDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<WaslaDbContext>>();
                services.RemoveAll<WaslaDbContext>();
                services.AddDbContext<WaslaDbContext>(options => options.UseSqlite(_connection));
            });
        }

        private async Task InitializeDatabaseAsync(bool seedSecurityData)
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
            await db.Database.EnsureCreatedAsync();
            if (!seedSecurityData)
            {
                return;
            }

            var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
            var now = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);

            foreach (var (id, name) in new[]
                     {
                         (SystemRoleIds.SuperAdmin, SystemRoleNames.SuperAdmin),
                         (SystemRoleIds.Doctor, SystemRoleNames.Doctor),
                         (SystemRoleIds.Reception, SystemRoleNames.Reception),
                         (SystemRoleIds.Patient, SystemRoleNames.Patient)
                     })
            {
                db.Roles.Add(Role.Create(id, name, true).Value);
            }

            foreach (var name in PermissionNames.All)
            {
                db.Permissions.Add(Permission.Create(SystemPermissionIds.For(name), name, true).Value);
            }

            foreach (var name in PermissionNames.All.Where(name => !PermissionNames.RootOnly.Contains(name) &&
                         name != PermissionNames.DoctorOnboardingViewOwn &&
                         name != PermissionNames.PatientProfileViewOwn))
            {
                db.RolePermissions.Add(new RolePermission(
                    Guid.NewGuid(),
                    SystemRoleIds.SuperAdmin,
                    SystemPermissionIds.For(name)));
            }

            db.RolePermissions.Add(new RolePermission(
                Guid.NewGuid(),
                SystemRoleIds.Doctor,
                SystemPermissionIds.For(PermissionNames.DoctorOnboardingViewOwn)));
            db.RolePermissions.Add(new RolePermission(
                Guid.NewGuid(),
                SystemRoleIds.Patient,
                SystemPermissionIds.For(PermissionNames.PatientProfileViewOwn)));

            var rootUser = ApplicationUser.Create(
                Guid.NewGuid(),
                "root",
                "root@example.test",
                null,
                await passwordService.HashAsync(RootPassword),
                UserType.SuperAdmin,
                false,
                now).Value;
            var root = SuperAdmin.Create(
                Guid.NewGuid(),
                rootUser.Id,
                "المشرف الجذر",
                "Root",
                true,
                null).Value;
            db.ApplicationUsers.Add(rootUser);
            db.SuperAdmins.Add(root);
            db.UserRoles.Add(new UserRole(Guid.NewGuid(), rootUser.Id, SystemRoleIds.SuperAdmin));
            await db.SaveChangesAsync();
        }
    }
}

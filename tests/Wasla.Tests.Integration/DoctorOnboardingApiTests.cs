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
using Wasla.Domain.Security;
using Wasla.Domain.Common;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Tests.Integration;

public sealed class DoctorOnboardingApiTests
{
    private const string RootPassword = "RootAdminPass123!";

    [Fact]
    public async Task PendingDoctor_CanCompleteLocationAndReviewedSpecializationFlow()
    {
        await using var factory = await OnboardingApiFactory.CreateAsync();
        using var anonymous = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        var governoratesResponse = await anonymous.GetAsync("/api/v1/public/governorates", testToken);
        Assert.Equal(HttpStatusCode.OK, governoratesResponse.StatusCode);
        var governorates = await ReadJsonAsync(governoratesResponse, testToken);
        Assert.Equal(27, governorates.GetArrayLength());
        var governorateId = governorates[0].GetProperty("id").GetInt32();

        var citiesResponse = await anonymous.GetAsync($"/api/v1/public/governorates/{governorateId}/cities", testToken);
        Assert.Equal(HttpStatusCode.OK, citiesResponse.StatusCode);
        var cities = await ReadJsonAsync(citiesResponse, testToken);
        var cityId = cities[0].GetProperty("id").GetInt32();
        var areasResponse = await anonymous.GetAsync($"/api/v1/public/cities/{cityId}/areas", testToken);
        Assert.Equal(HttpStatusCode.OK, areasResponse.StatusCode);
        var areaId = (await ReadJsonAsync(areasResponse, testToken))[0].GetProperty("id").GetInt32();

        using var registration = CreateDoctorRegistration();
        var registered = await anonymous.PostAsync("/api/v1/auth/doctors/register", registration, testToken);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        using var doctorClient = factory.CreateClient();
        doctorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await LoginAsync(doctorClient, "pending-doctor", "DoctorPass123!", testToken));
        var me = await doctorClient.GetFromJsonAsync<JsonElement>("/api/v1/auth/me", testToken);
        var permissions = me.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains(PermissionNames.DoctorOnboardingViewOwn, permissions);
        Assert.Contains(PermissionNames.DoctorSpecializationsViewOwn, permissions);
        Assert.Contains(PermissionNames.DoctorSpecializationsSubmitOwn, permissions);
        Assert.Contains(PermissionNames.DoctorSpecializationsResubmitOwn, permissions);
        Assert.Contains(PermissionNames.DoctorPracticeLocationViewOwn, permissions);
        Assert.Contains(PermissionNames.DoctorPracticeLocationManageOwn, permissions);
        Assert.DoesNotContain(PermissionNames.DoctorsViewAll, permissions);

        var optionsResponse = await doctorClient.GetAsync("/api/v1/doctors/me/specializations/options", testToken);
        Assert.Equal(HttpStatusCode.OK, optionsResponse.StatusCode);
        var options = await ReadJsonAsync(optionsResponse, testToken);
        Assert.Equal(47, options.GetArrayLength());
        var specializationId = options[0].GetProperty("id").GetGuid();
        var adjustedSpecializationId = options[1].GetProperty("id").GetGuid();

        var submittedResponse = await doctorClient.PostAsJsonAsync(
            "/api/v1/doctors/me/specialization-request",
            new { specializations = new[] { new { medicalSpecializationId = specializationId, isPrimary = true } } },
            testToken);
        Assert.Equal(HttpStatusCode.OK, submittedResponse.StatusCode);
        var duplicateOpenResponse = await doctorClient.PostAsJsonAsync(
            "/api/v1/doctors/me/specialization-request",
            new { specializations = new[] { new { medicalSpecializationId = specializationId, isPrimary = true } } },
            testToken);
        Assert.Equal(HttpStatusCode.Conflict, duplicateOpenResponse.StatusCode);

        var invalidCoordinatesResponse = await doctorClient.PutAsJsonAsync(
            "/api/v1/doctors/me/practice-location",
            new
            {
                governorateId,
                cityId,
                areaId,
                detailedAddress = "15 شارع الاختبار",
                latitude = 91m,
                longitude = 31.3301m,
                rowVersion = (string?)null
            },
            testToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidCoordinatesResponse.StatusCode);
        var invalidHierarchyResponse = await doctorClient.PutAsJsonAsync(
            "/api/v1/doctors/me/practice-location",
            new
            {
                governorateId = governorateId + 1,
                cityId,
                areaId,
                detailedAddress = "15 شارع الاختبار",
                latitude = 30.0561m,
                longitude = 31.3301m,
                rowVersion = (string?)null
            },
            testToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidHierarchyResponse.StatusCode);

        var locationResponse = await doctorClient.PutAsJsonAsync(
            "/api/v1/doctors/me/practice-location",
            new
            {
                governorateId,
                cityId,
                areaId,
                detailedAddress = "15 شارع الاختبار",
                latitude = 30.0561m,
                longitude = 31.3301m,
                rowVersion = (string?)null
            },
            testToken);
        Assert.True(
            locationResponse.StatusCode == HttpStatusCode.OK,
            await locationResponse.Content.ReadAsStringAsync(testToken));
        var createdLocation = await ReadJsonAsync(locationResponse, testToken);

        using var rootClient = factory.CreateClient();
        rootClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await LoginAsync(rootClient, "root", RootPassword, testToken));

        var createdCatalogResponse = await rootClient.PostAsJsonAsync(
            "/api/v1/admin/medical-specializations",
            new
            {
                nameAr = "تخصص اختبار",
                nameEn = "Test Specialty",
                descriptionAr = (string?)null,
                descriptionEn = (string?)null,
                sortOrder = 999
            },
            testToken);
        Assert.Equal(HttpStatusCode.OK, createdCatalogResponse.StatusCode);
        var createdCatalog = await ReadJsonAsync(createdCatalogResponse, testToken);
        var catalogId = createdCatalog.GetProperty("id").GetGuid();
        var originalCatalogRowVersion = createdCatalog.GetProperty("rowVersion").GetString()!;
        var deactivatedResponse = await rootClient.PostAsJsonAsync(
            $"/api/v1/admin/medical-specializations/{catalogId}/deactivate",
            new { rowVersion = originalCatalogRowVersion },
            testToken);
        Assert.Equal(HttpStatusCode.OK, deactivatedResponse.StatusCode);
        var deactivated = await ReadJsonAsync(deactivatedResponse, testToken);
        var staleCatalogUpdate = await rootClient.PutAsJsonAsync(
            $"/api/v1/admin/medical-specializations/{catalogId}",
            new
            {
                nameAr = "تخصص اختبار معدل",
                nameEn = "Updated Test Specialty",
                descriptionAr = (string?)null,
                descriptionEn = (string?)null,
                sortOrder = 999,
                rowVersion = originalCatalogRowVersion
            },
            testToken);
        Assert.Equal(HttpStatusCode.Conflict, staleCatalogUpdate.StatusCode);
        using var deleteCatalogRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/v1/admin/medical-specializations/{catalogId}")
        {
            Content = JsonContent.Create(new { rowVersion = deactivated.GetProperty("rowVersion").GetString() })
        };
        deleteCatalogRequest.Headers.Authorization = rootClient.DefaultRequestHeaders.Authorization;
        var deletedCatalogResponse = await rootClient.SendAsync(deleteCatalogRequest, testToken);
        Assert.Equal(HttpStatusCode.OK, deletedCatalogResponse.StatusCode);
        var deletedCatalog = await ReadJsonAsync(deletedCatalogResponse, testToken);
        var restoredCatalogResponse = await rootClient.PostAsJsonAsync(
            $"/api/v1/admin/medical-specializations/{catalogId}/restore",
            new { rowVersion = deletedCatalog.GetProperty("rowVersion").GetString() },
            testToken);
        Assert.Equal(HttpStatusCode.OK, restoredCatalogResponse.StatusCode);
        Assert.False((await ReadJsonAsync(restoredCatalogResponse, testToken)).GetProperty("isActive").GetBoolean());

        var queue = await rootClient.GetFromJsonAsync<JsonElement>(
            "/api/v1/admin/doctor-specialization-requests?status=PendingReview", testToken);
        var queuedRequest = queue.GetProperty("items")[0];
        var requestId = queuedRequest.GetProperty("requestId").GetGuid();
        var doctorId = queuedRequest.GetProperty("doctorId").GetGuid();
        var rowVersion = queuedRequest.GetProperty("rowVersion").GetString()!;
        var adjustedResponse = await rootClient.PutAsJsonAsync(
            $"/api/v1/admin/doctor-specialization-requests/{requestId}/specializations",
            new
            {
                specializations = new[] { new { medicalSpecializationId = adjustedSpecializationId, isPrimary = true } },
                reason = "تم التعديل طبقاً للمستندات.",
                rowVersion
            },
            testToken);
        Assert.Equal(HttpStatusCode.OK, adjustedResponse.StatusCode);
        var adjustedRequest = await ReadJsonAsync(adjustedResponse, testToken);
        Assert.Equal(2, adjustedRequest.GetProperty("currentRevisionNumber").GetInt32());
        const string modificationMessage = "برجاء مراجعة <التخصص> الأساسي.";
        var modificationResponse = await rootClient.PostAsJsonAsync(
            $"/api/v1/admin/doctor-specialization-requests/{requestId}/request-modification",
            new { message = modificationMessage, rowVersion = adjustedRequest.GetProperty("rowVersion").GetString() },
            testToken);
        Assert.Equal(HttpStatusCode.OK, modificationResponse.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
            var outbox = await db.EmailOutboxMessages.SingleAsync(
                item => item.IdempotencyKey == $"doctor-specialization-modification:{requestId}:revision-2",
                testToken);
            Assert.Contains("برجاء مراجعة &lt;التخصص&gt; الأساسي.", outbox.HtmlBody, StringComparison.Ordinal);
            Assert.DoesNotContain(modificationMessage, outbox.HtmlBody, StringComparison.Ordinal);
        }

        var open = await doctorClient.GetFromJsonAsync<JsonElement>(
            "/api/v1/doctors/me/specialization-request", testToken);
        Assert.Equal(modificationMessage, open.GetProperty("latestModificationMessage").GetString());
        var resubmittedResponse = await doctorClient.PostAsJsonAsync(
            "/api/v1/doctors/me/specialization-request/resubmit",
            new
            {
                specializations = new[] { new { medicalSpecializationId = specializationId, isPrimary = true } },
                rowVersion = open.GetProperty("rowVersion").GetString()
            },
            testToken);
        Assert.Equal(HttpStatusCode.OK, resubmittedResponse.StatusCode);

        var details = await rootClient.GetFromJsonAsync<JsonElement>(
            $"/api/v1/admin/doctor-specialization-requests/{requestId}", testToken);
        Assert.Equal(3, details.GetProperty("request").GetProperty("currentRevisionNumber").GetInt32());
        var approveResponse = await rootClient.PostAsJsonAsync(
            $"/api/v1/admin/doctor-specialization-requests/{requestId}/approve",
            new { rowVersion = details.GetProperty("request").GetProperty("rowVersion").GetString() },
            testToken);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        var effective = await doctorClient.GetFromJsonAsync<JsonElement>(
            "/api/v1/doctors/me/specializations", testToken);
        var effectiveItems = effective.GetProperty("items");
        Assert.Single(effectiveItems.EnumerateArray());
        Assert.True(effectiveItems[0].GetProperty("isPrimary").GetBoolean());
        var workflowHistory = await doctorClient.GetFromJsonAsync<JsonElement>(
            "/api/v1/doctors/me/specialization-request/history", testToken);
        Assert.Equal(5, workflowHistory.GetArrayLength());
        Assert.Equal(1, workflowHistory[1].GetProperty("previousRevisionNumber").GetInt32());
        Assert.Equal(2, workflowHistory[1].GetProperty("revisionNumber").GetInt32());

        var savedLocation = await doctorClient.GetFromJsonAsync<JsonElement>(
            "/api/v1/doctors/me/practice-location", testToken);
        Assert.Equal(areaId, savedLocation.GetProperty("area").GetProperty("id").GetInt32());

        var doctorDetails = await rootClient.GetFromJsonAsync<JsonElement>(
            $"/api/v1/admin/doctors/{doctorId}", testToken);
        var approveDoctorResponse = await rootClient.PostAsJsonAsync(
            $"/api/v1/admin/doctors/{doctorId}/approve",
            new { nationalId = "29801011234567", rowVersion = doctorDetails.GetProperty("rowVersion").GetString() },
            testToken);
        Assert.Equal(HttpStatusCode.OK, approveDoctorResponse.StatusCode);

        var updatedLocationResponse = await doctorClient.PutAsJsonAsync(
            "/api/v1/doctors/me/practice-location",
            new
            {
                governorateId,
                cityId,
                areaId,
                detailedAddress = "16 شارع الاختبار",
                latitude = 30.0562m,
                longitude = 31.3302m,
                rowVersion = createdLocation.GetProperty("rowVersion").GetString()
            },
            testToken);
        Assert.Equal(HttpStatusCode.OK, updatedLocationResponse.StatusCode);
        var staleLocationResponse = await doctorClient.PutAsJsonAsync(
            "/api/v1/doctors/me/practice-location",
            new
            {
                governorateId,
                cityId,
                areaId,
                detailedAddress = "17 شارع الاختبار",
                latitude = 30.0563m,
                longitude = 31.3303m,
                rowVersion = createdLocation.GetProperty("rowVersion").GetString()
            },
            testToken);
        Assert.Equal(HttpStatusCode.Conflict, staleLocationResponse.StatusCode);

        var changeRequestResponse = await doctorClient.PostAsJsonAsync(
            "/api/v1/doctors/me/specialization-request",
            new { specializations = new[] { new { medicalSpecializationId = adjustedSpecializationId, isPrimary = true } } },
            testToken);
        Assert.Equal(HttpStatusCode.OK, changeRequestResponse.StatusCode);
        var changeRequest = await ReadJsonAsync(changeRequestResponse, testToken);
        Assert.Equal("Change", changeRequest.GetProperty("type").GetString());
        var specializationToDeactivate = await rootClient.GetFromJsonAsync<JsonElement>(
            $"/api/v1/admin/medical-specializations/{adjustedSpecializationId}", testToken);
        var deactivateSelectedResponse = await rootClient.PostAsJsonAsync(
            $"/api/v1/admin/medical-specializations/{adjustedSpecializationId}/deactivate",
            new { rowVersion = specializationToDeactivate.GetProperty("rowVersion").GetString() },
            testToken);
        Assert.Equal(HttpStatusCode.OK, deactivateSelectedResponse.StatusCode);
        var unavailableApproval = await rootClient.PostAsJsonAsync(
            $"/api/v1/admin/doctor-specialization-requests/{changeRequest.GetProperty("requestId").GetGuid()}/approve",
            new { rowVersion = changeRequest.GetProperty("rowVersion").GetString() },
            testToken);
        Assert.Equal(HttpStatusCode.Conflict, unavailableApproval.StatusCode);
        var stillEffective = await doctorClient.GetFromJsonAsync<JsonElement>(
            "/api/v1/doctors/me/specializations", testToken);
        Assert.Equal(specializationId, stillEffective.GetProperty("items")[0].GetProperty("medicalSpecializationId").GetGuid());
        var rejectedChange = await rootClient.PostAsJsonAsync(
            $"/api/v1/admin/doctor-specialization-requests/{changeRequest.GetProperty("requestId").GetGuid()}/reject",
            new { reason = "المستندات الحالية لا تدعم التغيير.", rowVersion = changeRequest.GetProperty("rowVersion").GetString() },
            testToken);
        Assert.Equal(HttpStatusCode.OK, rejectedChange.StatusCode);
        var effectiveAfterRejection = await doctorClient.GetFromJsonAsync<JsonElement>(
            "/api/v1/doctors/me/specializations", testToken);
        Assert.Equal(specializationId, effectiveAfterRejection.GetProperty("items")[0].GetProperty("medicalSpecializationId").GetGuid());

        using var patientRegistration = CreatePatientRegistration();
        var registeredPatient = await anonymous.PostAsync(
            "/api/v1/auth/patients/register", patientRegistration, testToken);
        Assert.Equal(HttpStatusCode.Created, registeredPatient.StatusCode);
        using var patientClient = factory.CreateClient();
        patientClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await LoginAsync(patientClient, "patient-user", "PatientPass123!", testToken));
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await patientClient.GetAsync("/api/v1/doctors/me/specializations/options", testToken)).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
            var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
            var receptionUser = ApplicationUser.Create(
                Guid.NewGuid(),
                "reception-user",
                "reception-user@example.test",
                null,
                await passwords.HashAsync("ReceptionPass123!", testToken),
                UserType.Reception,
                false,
                DateTime.UtcNow).Value;
            db.ApplicationUsers.Add(receptionUser);
            db.UserRoles.Add(new UserRole(Guid.NewGuid(), receptionUser.Id, SystemRoleIds.Reception));
            await db.SaveChangesAsync(testToken);
        }
        using var receptionClient = factory.CreateClient();
        receptionClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await LoginAsync(receptionClient, "reception-user", "ReceptionPass123!", testToken));
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await receptionClient.GetAsync("/api/v1/doctors/me/practice-location", testToken)).StatusCode);
    }

    private static MultipartFormDataContent CreateDoctorRegistration()
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent("pending-doctor"), "UserName");
        content.Add(new StringContent("pending-doctor@example.test"), "Email");
        content.Add(new StringContent("01000000000"), "PhoneNumber");
        content.Add(new StringContent("DoctorPass123!"), "Password");
        content.Add(new StringContent("DoctorPass123!"), "ConfirmPassword");
        content.Add(new StringContent("طبيب اختبار"), "NameAr");
        content.Add(new StringContent("Test Doctor"), "NameEn");
        content.Add(new StringContent("1991-04-12"), "DateOfBirth");
        content.Add(new StringContent("Male"), "Gender");
        AddFile(content, "PersonalIdFrontImage", "front.png");
        AddFile(content, "PersonalIdBackImage", "back.png");
        AddFile(content, "SyndicateCardFrontImage", "syndicate.png");
        return content;
    }

    private static MultipartFormDataContent CreatePatientRegistration()
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent("patient-user"), "UserName");
        content.Add(new StringContent("patient-user@example.test"), "Email");
        content.Add(new StringContent("01000000001"), "PhoneNumber");
        content.Add(new StringContent("PatientPass123!"), "Password");
        content.Add(new StringContent("PatientPass123!"), "ConfirmPassword");
        content.Add(new StringContent("مريض اختبار"), "NameAr");
        content.Add(new StringContent("Test Patient"), "NameEn");
        content.Add(new StringContent("1995-06-20"), "DateOfBirth");
        content.Add(new StringContent("Female"), "Gender");
        return content;
    }

    private static void AddFile(MultipartFormDataContent content, string fieldName, string fileName)
    {
        var file = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, fieldName, fileName);
    }

    private static async Task<string> LoginAsync(
        HttpClient client,
        string identifier,
        string password,
        CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier, password }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadJsonAsync(response, cancellationToken)).GetProperty("accessToken").GetString()!;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        => (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).Clone();

    private sealed class OnboardingApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        private readonly string mediaRoot = Path.Combine(Path.GetTempPath(), $"wasla-onboarding-api-{Guid.NewGuid():N}");

        public static async Task<OnboardingApiFactory> CreateAsync()
        {
            var factory = new OnboardingApiFactory();
            await factory.connection.OpenAsync(TestContext.Current.CancellationToken);
            _ = factory.CreateClient();
            await factory.InitializeAsync();
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
                    ["MediaStorage:RootPath"] = mediaRoot,
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
                services.AddDbContext<WaslaDbContext>(options => options.UseSqlite(connection));
            });
        }

        private async Task InitializeAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            foreach (var typeName in new[]
                     {
                         "WaslaSecuritySeeder",
                         "MedicalSpecializationSeeder",
                         "EgyptLocationSeedCoordinator"
                     })
            {
                var type = typeof(WaslaDbContext).Assembly.GetType(
                    $"Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.{typeName}",
                    throwOnError: true)!;
                var seeder = scope.ServiceProvider.GetRequiredService(type);
                await (Task)type.GetMethod("SeedAsync")!.Invoke(
                    seeder,
                    [TestContext.Current.CancellationToken])!;
            }
        }
    }
}

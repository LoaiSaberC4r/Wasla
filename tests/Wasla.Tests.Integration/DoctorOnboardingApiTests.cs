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
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Security;
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

    [Fact]
    public async Task Doctor_can_manage_own_public_profile_but_not_another_doctors_qualifications()
    {
        await using var factory = await OnboardingApiFactory.CreateAsync();
        var testToken = TestContext.Current.CancellationToken;
        await SeedApprovedDoctorAsync(factory, "profile-doctor-a", "profile-a@example.test", testToken);
        await SeedApprovedDoctorAsync(factory, "profile-doctor-b", "profile-b@example.test", testToken);

        using var doctorA = factory.CreateClient();
        doctorA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await LoginAsync(doctorA, "profile-doctor-a", "DoctorPass123!", testToken));
        using var doctorB = factory.CreateClient();
        doctorB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await LoginAsync(doctorB, "profile-doctor-b", "DoctorPass123!", testToken));

        var profile = await doctorA.GetFromJsonAsync<JsonElement>("/api/v1/doctors/me/profile", testToken);
        var bioResponse = await doctorA.PutAsJsonAsync(
            "/api/v1/doctors/me/profile/bio",
            new { bio = "استشاري قلب", rowVersion = profile.GetProperty("rowVersion").GetString() },
            testToken);
        Assert.Equal(HttpStatusCode.OK, bioResponse.StatusCode);
        Assert.Equal("استشاري قلب", (await ReadJsonAsync(bioResponse, testToken)).GetProperty("bio").GetString());

        var createResponse = await doctorA.PostAsJsonAsync(
            "/api/v1/doctors/me/qualifications",
            new { nameAr = "دكتوراه", nameEn = (string?)null, displayOrder = 2 },
            testToken);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var qualification = await ReadJsonAsync(createResponse, testToken);
        var qualificationId = qualification.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await doctorB.PutAsJsonAsync(
            $"/api/v1/doctors/me/qualifications/{qualificationId}",
            new
            {
                nameAr = "محاولة تعديل",
                nameEn = "Denied",
                displayOrder = 1,
                rowVersion = qualification.GetProperty("rowVersion").GetString()
            },
            testToken)).StatusCode);

        var updateResponse = await doctorA.PutAsJsonAsync(
            $"/api/v1/doctors/me/qualifications/{qualificationId}",
            new
            {
                nameAr = "زمالة",
                nameEn = "Fellowship",
                displayOrder = 1,
                rowVersion = qualification.GetProperty("rowVersion").GetString()
            },
            testToken);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await ReadJsonAsync(updateResponse, testToken);
        using var delete = new HttpRequestMessage(
            HttpMethod.Delete, $"/api/v1/doctors/me/qualifications/{qualificationId}")
        {
            Content = JsonContent.Create(new
            {
                rowVersion = updated.GetProperty("rowVersion").GetString()
            })
        };
        Assert.Equal(HttpStatusCode.NoContent, (await doctorA.SendAsync(delete, testToken)).StatusCode);
        var qualifications = await doctorA.GetFromJsonAsync<JsonElement>(
            "/api/v1/doctors/me/qualifications", testToken);
        Assert.Empty(qualifications.EnumerateArray());
    }

    [Fact]
    public async Task OperationalPracticeApi_EnforcesOwnershipLifecycleSchedulingPricingAndReceptionScope()
    {
        await using var factory = await OnboardingApiFactory.CreateAsync();
        var testToken = TestContext.Current.CancellationToken;
        await SeedApprovedDoctorAsync(factory, "practice-doctor-a", "practice-a@example.test", testToken);
        await SeedApprovedDoctorAsync(factory, "practice-doctor-b", "practice-b@example.test", testToken);

        using var doctorA = factory.CreateClient();
        doctorA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await LoginAsync(doctorA, "practice-doctor-a", "DoctorPass123!", testToken));
        using var doctorB = factory.CreateClient();
        doctorB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await LoginAsync(doctorB, "practice-doctor-b", "DoctorPass123!", testToken));

        var governorates = await doctorA.GetFromJsonAsync<JsonElement>("/api/v1/public/governorates", testToken);
        var governorateId = governorates[0].GetProperty("id").GetInt32();
        var cities = await doctorA.GetFromJsonAsync<JsonElement>(
            $"/api/v1/public/governorates/{governorateId}/cities", testToken);
        var cityId = cities[0].GetProperty("id").GetInt32();
        var areas = await doctorA.GetFromJsonAsync<JsonElement>(
            $"/api/v1/public/cities/{cityId}/areas", testToken);
        var areaId = areas[0].GetProperty("id").GetInt32();

        var cairo = await CreatePracticeAsync(
            doctorA, "عيادة القاهرة", governorateId, cityId, areaId, testToken);
        var shebin = await CreatePracticeAsync(
            doctorA, "عيادة شبين", governorateId, cityId, areaId, testToken);
        var otherPractice = await CreatePracticeAsync(
            doctorB, "عيادة طبيب آخر", governorateId, cityId, areaId, testToken);
        var cairoId = cairo.GetProperty("id").GetGuid();
        var shebinId = shebin.GetProperty("id").GetGuid();
        var otherPracticeId = otherPractice.GetProperty("id").GetGuid();

        Assert.False(cairo.GetProperty("isActive").GetBoolean());
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await doctorB.GetAsync($"/api/v1/doctors/me/practices/{cairoId}", testToken)).StatusCode);

        var activationWithoutLogo = await doctorA.PostAsJsonAsync(
            $"/api/v1/doctors/me/practices/{cairoId}/activate",
            new { rowVersion = cairo.GetProperty("rowVersion").GetString() },
            testToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, activationWithoutLogo.StatusCode);

        var branding = await doctorA.GetFromJsonAsync<JsonElement>(
            $"/api/v1/doctors/me/practices/{cairoId}/branding", testToken);
        using var firstLogo = CreateLogoUpload(branding.GetProperty("rowVersion").GetString()!, "cairo.png");
        var firstLogoResponse = await doctorA.PostAsync(
            $"/api/v1/doctors/me/practices/{cairoId}/branding/logo", firstLogo, testToken);
        Assert.Equal(HttpStatusCode.OK, firstLogoResponse.StatusCode);
        var branded = await ReadJsonAsync(firstLogoResponse, testToken);

        using var replacementLogo = CreateLogoUpload(
            branded.GetProperty("rowVersion").GetString()!, "cairo-replacement.png");
        var replacementResponse = await doctorA.PostAsync(
            $"/api/v1/doctors/me/practices/{cairoId}/branding/logo", replacementLogo, testToken);
        Assert.Equal(HttpStatusCode.OK, replacementResponse.StatusCode);
        var replacedBranding = await ReadJsonAsync(replacementResponse, testToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await doctorA.GetAsync($"/api/v1/doctors/me/practices/{cairoId}/branding/logo", testToken)).StatusCode);

        var activatedResponse = await doctorA.PostAsJsonAsync(
            $"/api/v1/doctors/me/practices/{cairoId}/activate",
            new { rowVersion = cairo.GetProperty("rowVersion").GetString() },
            testToken);
        Assert.Equal(HttpStatusCode.OK, activatedResponse.StatusCode);
        var activated = await ReadJsonAsync(activatedResponse, testToken);
        Assert.True(activated.GetProperty("isActive").GetBoolean());

        using var removeLogo = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/v1/doctors/me/practices/{cairoId}/branding/logo")
        {
            Content = JsonContent.Create(new
            {
                rowVersion = replacedBranding.GetProperty("rowVersion").GetString()
            })
        };
        var removeLogoResponse = await doctorA.SendAsync(removeLogo, testToken);
        Assert.Equal(HttpStatusCode.Conflict, removeLogoResponse.StatusCode);

        var stalePracticeUpdate = await doctorA.PutAsJsonAsync(
            $"/api/v1/doctors/me/practices/{cairoId}",
            new
            {
                nameAr = "عيادة القاهرة المعدلة",
                nameEn = "Updated Cairo Practice",
                governorateId,
                cityId,
                areaId,
                detailedAddress = "15 شارع الاختبار",
                latitude = 30.0561m,
                longitude = 31.3301m,
                rowVersion = cairo.GetProperty("rowVersion").GetString()
            },
            testToken);
        Assert.Equal(HttpStatusCode.Conflict, stalePracticeUpdate.StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await doctorA.PostAsJsonAsync(
            $"/api/v1/doctors/me/practices/{cairoId}/schedule/periods",
            new { dayOfWeek = DayOfWeek.Saturday, startTime = "10:00:00", endTime = "14:00:00", slotDurationMinutes = 20 },
            testToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await doctorA.PostAsJsonAsync(
            $"/api/v1/doctors/me/practices/{cairoId}/schedule/periods",
            new { dayOfWeek = DayOfWeek.Saturday, startTime = "17:00:00", endTime = "21:00:00", slotDurationMinutes = 20 },
            testToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await doctorA.PostAsJsonAsync(
            $"/api/v1/doctors/me/practices/{cairoId}/schedule/periods",
            new { dayOfWeek = DayOfWeek.Saturday, startTime = "13:00:00", endTime = "16:00:00", slotDurationMinutes = 20 },
            testToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await doctorA.PostAsJsonAsync(
            $"/api/v1/doctors/me/practices/{shebinId}/schedule/periods",
            new { dayOfWeek = DayOfWeek.Saturday, startTime = "12:00:00", endTime = "15:00:00", slotDurationMinutes = 20 },
            testToken)).StatusCode);

        var segments = await doctorA.GetFromJsonAsync<JsonElement>(
            $"/api/v1/doctors/me/practices/{cairoId}/segments", testToken);
        var visitTypes = await doctorA.GetFromJsonAsync<JsonElement>(
            $"/api/v1/doctors/me/practices/{cairoId}/visit-types", testToken);
        Assert.Single(segments.EnumerateArray());
        Assert.True(segments[0].GetProperty("isDefault").GetBoolean());
        Assert.Equal(2, visitTypes.GetArrayLength());
        var priceRequest = new
        {
            segmentId = segments[0].GetProperty("id").GetGuid(),
            visitTypeId = visitTypes[0].GetProperty("id").GetGuid(),
            price = 500m
        };
        Assert.Equal(HttpStatusCode.Created, (await doctorA.PostAsJsonAsync(
            $"/api/v1/doctors/me/practices/{cairoId}/prices", priceRequest, testToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await doctorA.PostAsJsonAsync(
            $"/api/v1/doctors/me/practices/{cairoId}/prices", priceRequest, testToken)).StatusCode);

        var receptionResponse = await doctorA.PostAsJsonAsync(
            "/api/v1/doctors/me/receptions",
            new
            {
                userName = "scoped-reception",
                email = "scoped-reception@example.test",
                phoneNumber = "01000000009",
                temporaryPassword = "ReceptionPass123!",
                nameAr = "سارة الاستقبال",
                nameEn = "Sara Reception"
            },
            testToken);
        Assert.Equal(HttpStatusCode.Created, receptionResponse.StatusCode);
        var reception = await ReadJsonAsync(receptionResponse, testToken);
        var receptionId = reception.GetProperty("id").GetGuid();
        var searchPermissionId = SystemPermissionIds.For(PermissionNames.PatientsSearchBasic);
        var cairoAssignmentRequest = new
        {
            doctorPracticeId = cairoId,
            permissionIds = new[] { searchPermissionId }
        };
        var cairoAssignmentResponse = await doctorA.PostAsJsonAsync(
            $"/api/v1/doctors/me/receptions/{receptionId}/assignments",
            cairoAssignmentRequest,
            testToken);
        Assert.Equal(HttpStatusCode.Created, cairoAssignmentResponse.StatusCode);
        var cairoAssignment = await ReadJsonAsync(cairoAssignmentResponse, testToken);
        Assert.Equal(HttpStatusCode.Conflict, (await doctorA.PostAsJsonAsync(
            $"/api/v1/doctors/me/receptions/{receptionId}/assignments",
            cairoAssignmentRequest,
            testToken)).StatusCode);

        using var receptionClient = factory.CreateClient();
        receptionClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await LoginAsync(
                receptionClient, "scoped-reception", "ReceptionPass123!", testToken));
        var changedPassword = await receptionClient.PostAsJsonAsync(
            "/api/v1/auth/change-password",
            new
            {
                currentPassword = "ReceptionPass123!",
                newPassword = "ReceptionPass456!",
                confirmPassword = "ReceptionPass456!"
            },
            testToken);
        Assert.Equal(HttpStatusCode.NoContent, changedPassword.StatusCode);
        receptionClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await LoginAsync(
                receptionClient, "scoped-reception", "ReceptionPass456!", testToken));
        var receptionMe = await receptionClient.GetFromJsonAsync<JsonElement>("/api/v1/auth/me", testToken);
        Assert.DoesNotContain(
            receptionMe.GetProperty("permissions").EnumerateArray(),
            item => item.GetString()?.Contains("Revenue", StringComparison.OrdinalIgnoreCase) == true);

        Assert.Equal(HttpStatusCode.OK, (await receptionClient.GetAsync(
            $"/api/v1/patients/search?doctorPracticeId={cairoId}", testToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await receptionClient.GetAsync(
            $"/api/v1/patients/search?doctorPracticeId={shebinId}", testToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await receptionClient.GetAsync(
            $"/api/v1/patients/search?doctorPracticeId={otherPracticeId}", testToken)).StatusCode);

        var assignmentId = cairoAssignment.GetProperty("id").GetGuid();
        var deactivatedAssignmentResponse = await doctorA.PostAsJsonAsync(
            $"/api/v1/doctors/me/receptions/{receptionId}/assignments/{assignmentId}/deactivate",
            new { rowVersion = cairoAssignment.GetProperty("rowVersion").GetString() },
            testToken);
        Assert.Equal(HttpStatusCode.OK, deactivatedAssignmentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await receptionClient.GetAsync(
            $"/api/v1/patients/search?doctorPracticeId={cairoId}", testToken)).StatusCode);
        var deactivatedAssignment = await ReadJsonAsync(deactivatedAssignmentResponse, testToken);
        var reactivatedAssignmentResponse = await doctorA.PostAsJsonAsync(
            $"/api/v1/doctors/me/receptions/{receptionId}/assignments/{assignmentId}/activate",
            new { rowVersion = deactivatedAssignment.GetProperty("rowVersion").GetString() },
            testToken);
        Assert.Equal(HttpStatusCode.OK, reactivatedAssignmentResponse.StatusCode);
        var reactivatedAssignment = await ReadJsonAsync(reactivatedAssignmentResponse, testToken);

        var paymentPermissionId = SystemPermissionIds.For(PermissionNames.PracticePaymentsRecord);
        var wrongPermissionResponse = await doctorA.PutAsJsonAsync(
            $"/api/v1/doctors/me/receptions/{receptionId}/assignments/{assignmentId}",
            new
            {
                permissionIds = new[] { paymentPermissionId },
                rowVersion = reactivatedAssignment.GetProperty("rowVersion").GetString()
            },
            testToken);
        Assert.Equal(HttpStatusCode.OK, wrongPermissionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await receptionClient.GetAsync(
            $"/api/v1/patients/search?doctorPracticeId={cairoId}", testToken)).StatusCode);
        var wrongPermissionAssignment = await ReadJsonAsync(wrongPermissionResponse, testToken);
        var restoredPermissionResponse = await doctorA.PutAsJsonAsync(
            $"/api/v1/doctors/me/receptions/{receptionId}/assignments/{assignmentId}",
            new
            {
                permissionIds = new[] { searchPermissionId },
                rowVersion = wrongPermissionAssignment.GetProperty("rowVersion").GetString()
            },
            testToken);
        Assert.Equal(HttpStatusCode.OK, restoredPermissionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await receptionClient.GetAsync(
            $"/api/v1/patients/search?doctorPracticeId={cairoId}", testToken)).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
            var receptionUser = await db.ApplicationUsers.SingleAsync(
                item => item.UserName == "scoped-reception", testToken);
            receptionUser.Deactivate();
            await db.SaveChangesAsync(testToken);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await receptionClient.GetAsync(
            $"/api/v1/patients/search?doctorPracticeId={cairoId}", testToken)).StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
            var receptionUser = await db.ApplicationUsers.SingleAsync(
                item => item.UserName == "scoped-reception", testToken);
            receptionUser.Activate();
            var doctorUser = await db.ApplicationUsers.SingleAsync(
                item => item.UserName == "practice-doctor-a", testToken);
            doctorUser.Deactivate();
            await db.SaveChangesAsync(testToken);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await receptionClient.GetAsync(
            $"/api/v1/patients/search?doctorPracticeId={cairoId}", testToken)).StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
            var doctorUser = await db.ApplicationUsers.SingleAsync(
                item => item.UserName == "practice-doctor-a", testToken);
            doctorUser.Activate();
            var doctor = await db.Doctors.SingleAsync(
                item => item.ApplicationUserId == doctorUser.Id, testToken);
            Assert.True(doctor.Suspend("closure test", doctorUser.Id, DateTime.UtcNow).IsSuccess);
            await db.SaveChangesAsync(testToken);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await receptionClient.GetAsync(
            $"/api/v1/patients/search?doctorPracticeId={cairoId}", testToken)).StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
            var doctorUser = await db.ApplicationUsers.SingleAsync(
                item => item.UserName == "practice-doctor-a", testToken);
            var doctor = await db.Doctors.SingleAsync(
                item => item.ApplicationUserId == doctorUser.Id, testToken);
            Assert.True(doctor.Reactivate(doctorUser.Id, DateTime.UtcNow).IsSuccess);
            await db.SaveChangesAsync(testToken);
        }
        Assert.Equal(HttpStatusCode.OK, (await receptionClient.GetAsync(
            $"/api/v1/patients/search?doctorPracticeId={cairoId}", testToken)).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await doctorA.PostAsJsonAsync(
            $"/api/v1/doctors/me/receptions/{receptionId}/assignments",
            new { doctorPracticeId = shebinId, permissionIds = new[] { searchPermissionId } },
            testToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await receptionClient.GetAsync(
            $"/api/v1/patients/search?doctorPracticeId={shebinId}", testToken)).StatusCode);
        var receptionPractices = await receptionClient.GetFromJsonAsync<JsonElement>(
            "/api/v1/reception/practices", testToken);
        Assert.Equal(2, receptionPractices.GetArrayLength());
    }

    private static async Task SeedApprovedDoctorAsync(
        OnboardingApiFactory factory,
        string userName,
        string email,
        CancellationToken cancellationToken)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var userId = Guid.NewGuid();
        var user = ApplicationUser.Create(
            userId,
            userName,
            email,
            null,
            await passwords.HashAsync("DoctorPass123!", cancellationToken),
            UserType.Doctor,
            false,
            DateTime.UtcNow).Value;
        var doctor = Doctor.Create(
            Guid.NewGuid(), userId, "طبيب عيادات", "Practice Doctor", new DateOnly(1990, 1, 1),
            Gender.Male, null, "front.png", "back.png", "syndicate.png", null,
            DateOnly.FromDateTime(DateTime.UtcNow)).Value;
        var nationalId = userName.EndsWith('a') ? "29801011234567" : "29801011234568";
        Assert.True(doctor.Approve(nationalId, userId, DateTime.UtcNow).IsSuccess);

        db.ApplicationUsers.Add(user);
        db.Doctors.Add(doctor);
        db.UserRoles.Add(new UserRole(Guid.NewGuid(), userId, SystemRoleIds.Doctor));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<JsonElement> CreatePracticeAsync(
        HttpClient client,
        string nameAr,
        int governorateId,
        int cityId,
        int areaId,
        CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/doctors/me/practices",
            new
            {
                nameAr,
                nameEn = "Test Practice",
                governorateId,
                cityId,
                areaId,
                detailedAddress = "15 شارع الاختبار",
                latitude = 30.0561m,
                longitude = 31.3301m
            },
            cancellationToken);
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            await response.Content.ReadAsStringAsync(cancellationToken));
        return await ReadJsonAsync(response, cancellationToken);
    }

    private static MultipartFormDataContent CreateLogoUpload(string rowVersion, string fileName)
    {
        var content = new MultipartFormDataContent();
        AddFile(content, "Logo", fileName);
        content.Add(new StringContent(rowVersion), "RowVersion");
        return content;
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

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Features.Families;
using Wasla.Domain.Clinical;
using Wasla.Domain.Families;
using Wasla.Domain.Security;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase15DiagnosticApiTests;
using static Wasla.Tests.Integration.Phase16FamilyFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase16GuardianActivationTests
{
    private const string Dependents = "/api/v1/families/mine/medical-record-dependents";

    [Theory]
    [InlineData(FamilyMemberRole.Father, false)]
    [InlineData(FamilyMemberRole.Mother, false)]
    [InlineData(FamilyMemberRole.Guardian, false)]
    [InlineData(FamilyMemberRole.LegalGuardian, false)]
    [InlineData(FamilyMemberRole.LegalGuardian, true)]
    public async Task Configuration_activated_reads_use_role_specific_exact_pair_authority(FamilyMemberRole role, bool sql)
    {
        await using var f = await Phase16FamilyFixture.CreateAsync(sql, approve: false, adultRole: role);
        var routes = await SeedRecordAsync(f);
        // A separate host binds the base sharing flag and explicit TEST-ONLY policy through configuration.
        // No options mutation or PostConfigure enables sharing on this host.
        await using var node = f.App.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(TestPolicy())));
        var sharing = node.Services.GetRequiredService<IOptions<MedicalRecordOptions>>().Value;
        Assert.True(sharing.GuardianSharingEnabled);
        Assert.True(node.Services.GetRequiredService<IOptions<FamilyMedicalPolicyOptions>>().Value.IsConfigured);
        using var adult = node.CreateClient(); adult.DefaultRequestHeaders.Authorization = f.Father.DefaultRequestHeaders.Authorization;
        using var admin = node.CreateClient(); admin.DefaultRequestHeaders.Authorization = f.Admin.DefaultRequestHeaders.Authorization;
        var guardian = role is FamilyMemberRole.Guardian or FamilyMemberRole.LegalGuardian;
        if (guardian)
        {
            await AssertUnavailableAsync(adult, f.ChildId, routes);
            var review = await ReviewAsync(f, f.App.PatientId);
            await OkAsync(await PostAsync(admin, Eligibility + $"/{review.GetProperty("reviewId").GetGuid()}/approve", new
            {
                rowVersion = review.GetProperty("rowVersion").GetString(), policyVersion = "TEST-ONLY-v1", basisReference = "TEST-reviewed-proof",
                authorityRelationshipRequestId = await f.AuthorityAsync(review.GetProperty("reviewId").GetGuid())
            }));
        }
        foreach (var route in routes)
        {
            using var read = await adult.GetAsync(route, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            Assert.True(read.Headers.CacheControl?.NoStore);
            if (route.EndsWith("/content", StringComparison.Ordinal))
                Assert.Equal("nosniff", read.Headers.GetValues("X-Content-Type-Options").Single());
            else
            {
                var json = await read.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                Assert.DoesNotContain("clinicalNotes", json);
                Assert.DoesNotContain("notes", json, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("mediaKey", json);
                Assert.DoesNotContain("Internal", json, StringComparison.OrdinalIgnoreCase);
            }
        }
        var dependents = (await GetAsync(adult, Dependents)).GetProperty("items");
        Assert.Equal(guardian ? 1 : 2, dependents.GetArrayLength());
        Assert.Contains(dependents.EnumerateArray(), p => p.GetProperty("patientId").GetGuid() == f.ChildId);
        await f.App.WithDbAsync(async db =>
        {
            Assert.Equal(sql, db.Database.IsSqlServer());
            Assert.Equal(5, await db.Set<MedicalRecordAccessAudit>().CountAsync(a => a.PatientId == f.ChildId && a.ActorCategory == "Dependent"));
            Assert.Equal(4, await db.FamilyMembers.CountAsync());
            Assert.Equal(guardian ? 1 : 0, await db.Set<FamilyMedicalEligibilityReview>().CountAsync(r => r.Status == FamilyMedicalEligibilityStatus.Approved));
        });
        foreach (var user in new[] { "reception", "admin", "root", "doctor" })
        {
            using var authenticated = await f.App.ClientAsync(user);
            using var staff = node.CreateClient(); staff.DefaultRequestHeaders.Authorization = authenticated.DefaultRequestHeaders.Authorization;
            await AssertUnavailableAsync(staff, f.ChildId, routes);
        }
    }

    [Theory]
    [InlineData("sharing")]
    [InlineData("approval")]
    [InlineData("master")]
    [InlineData("version")]
    [InlineData("basis")]
    [InlineData("timezone")]
    [InlineData("eligibility")]
    [InlineData("permission")]
    [InlineData("account")]
    [InlineData("link")]
    [InlineData("family")]
    [InlineData("adult")]
    [InlineData("child")]
    [InlineData("relationship")]
    [InlineData("role")]
    [InlineData("age")]
    public async Task Every_route_and_attachment_revalidates_current_guardian_authority(string gate)
    {
        await using var f = await Phase16FamilyFixture.CreateAsync(adultRole: FamilyMemberRole.Guardian);
        var routes = await SeedRecordAsync(f);
        foreach (var route in routes) { using var read = await f.Father.GetAsync(route, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, read.StatusCode); }
        var policy = f.App.Services.GetRequiredService<IOptions<FamilyMedicalPolicyOptions>>().Value;
        switch (gate)
        {
            case "sharing": f.App.Services.GetRequiredService<IOptions<MedicalRecordOptions>>().Value.GuardianSharingEnabled = false; break;
            case "approval": policy.Approved = false; break;
            case "master": policy.MasterSourceOfTruthAdopted = false; break;
            case "version": policy.PolicyVersion = "TEST-ONLY-v2"; break;
            case "basis": policy.BasisReferences = ["TEST-other-basis"]; break;
            case "timezone": policy.TimeZoneId = "TEST/Invalid"; break;
            case "eligibility":
                var review = await ReviewAsync(f, f.App.PatientId);
                await f.DecideEligibilityAsync(review.GetProperty("reviewId").GetGuid(), "revoke", review.GetProperty("rowVersion").GetString()!);
                break;
            case "age":
                DateOnly birthday = default;
                await f.App.WithDbAsync(async db => birthday = (await db.Patients.SingleAsync(p => p.Id == f.ChildId)).DateOfBirth.AddYears(16));
                var boundary = TimeZoneInfo.ConvertTimeToUtc(birthday.ToDateTime(TimeOnly.MinValue), TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"));
                f.App.Clock.UtcNow = boundary.AddTicks(-1);
                foreach (var route in routes) { using var read = await f.Father.GetAsync(route, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, read.StatusCode); }
                f.App.Clock.UtcNow = boundary;
                break;
            default:
                await f.App.WithDbAsync(async db =>
                {
                    switch (gate)
                    {
                        case "permission": db.RolePermissions.Remove(await db.RolePermissions.SingleAsync(r => r.RoleId == SystemRoleIds.Patient && r.PermissionId == SystemPermissionIds.For(PermissionNames.MedicalRecordsViewDependent))); break;
                        case "account": (await db.ApplicationUsers.SingleAsync(u => u.UserName == "patient")).Deactivate(); break;
                        case "link": db.PatientAccountLinks.Remove(await db.PatientAccountLinks.SingleAsync(l => l.PatientId == f.App.PatientId)); break;
                        case "family": db.Entry(await db.Families.SingleAsync()).Property(p => p.Status).CurrentValue = FamilyStatus.Inactive; break;
                        case "adult": db.Entry(await db.FamilyMembers.SingleAsync(m => m.PatientId == f.App.PatientId)).Property(m => m.IsActive).CurrentValue = false; break;
                        case "child": db.Entry(await db.FamilyMembers.SingleAsync(m => m.PatientId == f.ChildId)).Property(m => m.IsActive).CurrentValue = false; break;
                        case "role": db.Entry(await db.FamilyMembers.SingleAsync(m => m.PatientId == f.App.PatientId)).Property(m => m.Role).CurrentValue = FamilyMemberRole.Child; break;
                        case "relationship":
                            var member = await db.FamilyMembers.SingleAsync(m => m.PatientId == f.ChildId);
                            db.Entry(await db.FamilyRelationshipRequests.SingleAsync(r => r.Id == member.AddedThroughRequestId)).Property(r => r.Status).CurrentValue = FamilyRelationshipRequestStatus.Rejected;
                            break;
                        default: throw new InvalidOperationException(gate);
                    }
                    await db.SaveChangesAsync();
                });
                break;
        }
        await AssertUnavailableAsync(f.Father, f.ChildId, routes);
    }

    [Fact]
    public async Task Restoring_a_restriction_does_not_restore_revoked_medical_eligibility()
    {
        await using var f = await Phase16FamilyFixture.CreateAsync(otherAdultRole: FamilyMemberRole.Guardian);
        var routes = await SeedRecordAsync(f);
        using var submitted = await SubmitAsync(f.Father, f.ChildId, f.App.OtherPatientId);
        var request = await JsonAsync(submitted);
        await f.DecideAsync(request.GetProperty("requestId").GetGuid(), "approve", request.GetProperty("rowVersion").GetString()!);
        await AssertUnavailableAsync(f.Mother, f.ChildId, routes);
        using var restoration = await SubmitAsync(f.Mother, f.ChildId, f.App.OtherPatientId, "Restoration");
        request = await JsonAsync(restoration);
        var review = await ReviewAsync(f, f.App.OtherPatientId);
        await f.DecideEligibilityAsync(review.GetProperty("reviewId").GetGuid(), "revoke", review.GetProperty("rowVersion").GetString()!);
        await f.DecideAsync(request.GetProperty("requestId").GetGuid(), "approve", request.GetProperty("rowVersion").GetString()!);
        await AssertUnavailableAsync(f.Mother, f.ChildId, routes);
        await f.App.WithDbAsync(async db => Assert.Equal(FamilyMedicalRestrictionStatus.Lifted, (await db.Set<FamilyMedicalAccessRestriction>().SingleAsync()).Status));
    }

    [Fact]
    public async Task Base_activation_without_approved_policy_keeps_all_dependent_routes_unavailable()
    {
        await using var f = await Phase16FamilyFixture.CreateAsync();
        var routes = await SeedRecordAsync(f);
        await using var node = f.App.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FamilyMedicalPolicy:Approved"] = "false", ["FamilyMedicalPolicy:MasterSourceOfTruthAdopted"] = "false"
        })));
        Assert.True(node.Services.GetRequiredService<IOptions<MedicalRecordOptions>>().Value.GuardianSharingEnabled);
        using var adult = node.CreateClient(); adult.DefaultRequestHeaders.Authorization = f.Father.DefaultRequestHeaders.Authorization;
        await AssertUnavailableAsync(adult, f.ChildId, routes);
    }

    private static Dictionary<string, string?> TestPolicy() => new()
    {
        ["FamilyMedicalPolicy:Approved"] = "true", ["FamilyMedicalPolicy:MasterSourceOfTruthAdopted"] = "true",
        ["FamilyMedicalPolicy:PolicyVersion"] = "TEST-ONLY-v1", ["FamilyMedicalPolicy:ApprovalReference"] = "TEST fixture: no legal approval",
        ["FamilyMedicalPolicy:Jurisdiction"] = "TEST", ["FamilyMedicalPolicy:TimeZoneId"] = "Africa/Cairo",
        ["FamilyMedicalPolicy:BasisReferences:0"] = "TEST-reviewed-proof",
        ["FamilyMedicalPolicy:AllowedDocumentTypes:0"] = "RequesterNationalIdFront"
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Eligibility_approval_requires_current_allowed_relationship_proof(bool missing)
    {
        await using var f = await Phase16FamilyFixture.CreateAsync(approve: false, adultRole: FamilyMemberRole.Guardian);
        var routes = await SeedRecordAsync(f);
        if (missing)
            await f.App.WithDbAsync(db => db.FamilyRelationshipDocuments.ExecuteDeleteAsync(TestContext.Current.CancellationToken));
        else f.App.Services.GetRequiredService<IOptions<FamilyMedicalPolicyOptions>>().Value.AllowedDocumentTypes = [FamilyRelationshipDocumentType.BirthCertificate];
        var review = await ReviewAsync(f, f.App.PatientId);
        using var denied = await PostAsync(f.Admin, Eligibility + $"/{review.GetProperty("reviewId").GetGuid()}/approve", new
        {
            rowVersion = review.GetProperty("rowVersion").GetString(), policyVersion = "TEST-ONLY-v1", basisReference = "TEST-reviewed-proof",
            authorityRelationshipRequestId = await f.AuthorityAsync(review.GetProperty("reviewId").GetGuid())
        });
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        await AssertUnavailableAsync(f.Father, f.ChildId, routes);
        await f.App.WithDbAsync(async db => Assert.All(await db.Set<FamilyMedicalEligibilityReview>().ToArrayAsync(), r => Assert.Equal(FamilyMedicalEligibilityStatus.PendingReview, r.Status)));
    }

    private static async Task<System.Text.Json.JsonElement> ReviewAsync(Phase16FamilyFixture f, Guid adult)
        => (await GetAsync(f.Admin, Eligibility + "/reviews")).GetProperty("items").EnumerateArray()
            .Single(r => r.GetProperty("adultPatientId").GetGuid() == adult && r.GetProperty("childPatientId").GetGuid() == f.ChildId);

    private static async Task AssertUnavailableAsync(HttpClient client, Guid child, string[] routes)
    {
        foreach (var route in routes)
        {
            using var denied = await client.GetAsync(route, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }
        using var dependents = await client.GetAsync(Dependents, TestContext.Current.CancellationToken);
        if (dependents.StatusCode == HttpStatusCode.OK)
            Assert.DoesNotContain((await JsonAsync(dependents)).GetProperty("items").EnumerateArray(), p => p.GetProperty("patientId").GetGuid() == child);
        else Assert.Equal(HttpStatusCode.NotFound, dependents.StatusCode);
    }

    internal static async Task<string[]> SeedRecordAsync(Phase16FamilyFixture f)
    {
        using var doctor = await f.App.ClientAsync("doctor"); using var reception = await f.App.ClientAsync("reception");
        var ticket = await f.App.StartAsync(doctor, await f.App.WalkInAsync(reception, patientId: f.ChildId));
        var encounter = ticket.GetProperty("medicalEncounterId").GetGuid();
        var lab = await AddAsync(f.App, doctor, encounter, "lab", "Child test");
        await CompleteAsync(f.App, doctor, ticket, lab);
        var detail = await GetAsync(doctor, f.App.EncounterUrl(encounter));
        await OkAsync(await PostAsync(doctor, f.App.EncounterUrl(encounter) + "/amendments", new
        {
            reason = "Internal amendment reason", encounterRowVersion = detail.GetProperty("rowVersion").GetString(),
            changes = new[] { new { target = "Diagnosis", changeType = "Added", type = "Primary", displayText = "Safe diagnosis", notes = "Internal diagnosis note" } }
        }));
        await Phase16Fixture.HideAsync(f.App, doctor, encounter);
        var order = lab.GetProperty("requestId").GetGuid();
        lab = await GetAsync(doctor, $"/api/v1/doctors/me/lab-requests/{order}");
        using var uploaded = await UploadAsync(doctor, $"/api/v1/doctors/me/lab-requests/{order}/results", "lab",
            lab.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("itemId").GetGuid()), lab.GetProperty("rowVersion").GetString());
        Assert.True(uploaded.IsSuccessStatusCode, await uploaded.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var result = (await JsonAsync(uploaded)).GetProperty("result"); var resultId = result.GetProperty("resultId").GetGuid();
        var attachment = result.GetProperty("current").GetProperty("attachments")[0].GetProperty("attachmentId").GetGuid();
        return [f.Record() + "/overview", f.Record() + "/timeline", f.Record() + $"/encounters/{encounter}",
            f.Record() + $"/items/lab-result/{resultId}", f.Record() + $"/items/lab-result/{resultId}/attachments/{attachment}/content"];
    }
}

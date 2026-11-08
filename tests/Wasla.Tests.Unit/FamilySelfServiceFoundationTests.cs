using System.Text.Json;
using Wasla.Application.Features.Families;
using Wasla.Domain.Families;
using Wasla.Domain.Security;

namespace Wasla.Tests.Unit;

public sealed class FamilySelfServiceFoundationTests
{
    [Fact]
    public void All_371_pre_closure_permission_ids_remain_frozen_in_original_order()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Wasla.sln"))) root = root.Parent;
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root!.FullName, "docs/family-self-service/permission-id-baseline-2026-11-08.json")));
        var frozen = json.RootElement.EnumerateArray().ToArray(); Assert.Equal(371, frozen.Length);
        foreach (var permission in frozen)
        {
            var index = permission.GetProperty("index").GetInt32(); var name = permission.GetProperty("name").GetString()!;
            Assert.Equal(name, PermissionNames.All[index]); Assert.Equal(permission.GetProperty("id").GetGuid(), SystemPermissionIds.For(name));
        }
    }
    [Fact]
    public void All_pre_epic_permission_names_keep_their_index_and_guid()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Wasla.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        using var baseline = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir.FullName, "docs/family-self-service/baseline-permissions.json")));
        foreach (var old in baseline.RootElement.EnumerateArray())
        {
            var name = old.GetProperty("name").GetString()!;
            Assert.Equal(name, PermissionNames.All[old.GetProperty("index").GetInt32()]);
            Assert.Equal(old.GetProperty("id").GetGuid(), SystemPermissionIds.For(name));
        }
        using var added = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir.FullName, "docs/family-self-service/new-permissions.json")));
        foreach (var current in added.RootElement.EnumerateArray())
        {
            var name = current.GetProperty("name").GetString()!;
            Assert.Equal(name, PermissionNames.All[current.GetProperty("index").GetInt32()]);
            Assert.Equal(current.GetProperty("id").GetGuid(), SystemPermissionIds.For(name));
        }
        Assert.Equal(PermissionNames.All.Count, PermissionNames.All.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("2026-11-06T21:59:59Z", "2026-11-06")]
    [InlineData("2026-11-06T22:00:00Z", "2026-11-07")]
    public void Operational_day_uses_Cairo_midnight_independently_of_signed_medical_policy(string utc, string date)
        => Assert.Equal(DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture), new DependentManagementOptions().Today(DateTime.Parse(utc, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal)));

    [Fact]
    public void Operational_proof_binds_both_members_and_preserves_revocation()
    {
        var now = new DateTime(2026, 11, 7, 8, 0, 0, DateTimeKind.Utc);
        var admin = Guid.NewGuid(); var parentId = Guid.NewGuid(); var childId = Guid.NewGuid();
        var family = Family.Create(Guid.NewGuid(), admin).Value;
        var request = FamilyRelationshipRequest.Create(Guid.NewGuid(), FamilyRelationshipRequestType.CreateFamily, null, parentId,
            childId, FamilyMemberRole.Father, FamilyMemberRole.Child, Guid.NewGuid(), now).Value;
        request.AssignCreatedFamily(family.Id); request.Approve(admin, now);
        var parent = family.AddMember(Guid.NewGuid(), parentId, FamilyMemberRole.Father, now, admin, request.Id).Value;
        var child = family.AddMember(Guid.NewGuid(), childId, FamilyMemberRole.Child, now, admin, request.Id).Value;
        var authority = ParentChildOperationalAuthority.Approve(parent, child, request);
        Assert.Equal(parent.Id, authority.ParentFamilyMemberId); Assert.Equal(child.Id, authority.ChildFamilyMemberId);
        Assert.Equal(request.CurrentRevisionNumber, authority.ApprovedRevisionNumber);
        Assert.Throws<ArgumentException>(() => ParentChildOperationalAuthority.Approve(child, parent, request));
        authority.Revoke(admin, now.AddMinutes(1), "Reviewed revocation");
        Assert.False(authority.IsActive); Assert.NotNull(authority.RevokedAtUtc);
        Assert.Throws<ArgumentException>(() => authority.Revoke(admin, now.AddMinutes(2), "Replay"));
    }
}

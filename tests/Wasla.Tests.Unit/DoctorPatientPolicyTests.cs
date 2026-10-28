using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlock.Application.Abstraction.Security;
using Wasla.Application;
using Wasla.Application.Features.DoctorPatients;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Practices;
using Wasla.Domain.Security;

namespace Wasla.Tests.Unit;

public sealed class DoctorPatientPolicyTests
{
    private static readonly string[] Grants = [PermissionNames.DoctorPatientsViewOwn,
        PermissionNames.DoctorPatientsViewHistoricalOwn, PermissionNames.PrescriptionsViewOwn];
    [Theory]
    [InlineData(UserType.Reception, DoctorApprovalStatus.Approved, true, false, false)]
    [InlineData(UserType.Patient, DoctorApprovalStatus.Approved, true, false, false)]
    [InlineData(UserType.SuperAdmin, DoctorApprovalStatus.Approved, true, false, false)]
    [InlineData(UserType.Doctor, DoctorApprovalStatus.Pending, true, false, false)]
    [InlineData(UserType.Doctor, DoctorApprovalStatus.Rejected, true, false, false)]
    [InlineData(UserType.Doctor, DoctorApprovalStatus.Suspended, true, false, false)]
    [InlineData(UserType.Doctor, DoctorApprovalStatus.Approved, false, false, false)]
    [InlineData(UserType.Doctor, DoctorApprovalStatus.Approved, true, true, false)]
    [InlineData(UserType.Doctor, DoctorApprovalStatus.Approved, true, false, true)]
    public async Task Actor_state_is_checked_against_current_database(UserType type, DoctorApprovalStatus status, bool active, bool firstLogin, bool expected)
    {
        var (policy, _, proxy) = Setup(type, status, active, firstLogin);
        var result = await policy.ActorAsync(PermissionNames.DoctorPatientsViewOwn, TestContext.Current.CancellationToken);
        Assert.Equal(expected, result.IsSuccess);
        proxy.Snapshot = proxy.Snapshot with { Permissions = [] };
        Assert.True((await policy.ActorAsync(PermissionNames.DoctorPatientsViewOwn, TestContext.Current.CancellationToken)).IsFailure);
    }
    [Fact]
    public async Task Relationship_and_historical_scope_are_independent_of_role_and_patient_identifier()
    {
        var (policy, history, proxy) = Setup();
        Assert.True((await policy.AuthorizeAsync(Guid.NewGuid(), TestContext.Current.CancellationToken)).IsSuccess);
        var scope = await history.AuthorizeOwnReadAsync(proxy.Practice.Id, PermissionNames.PrescriptionsViewOwn, Guid.NewGuid(), TestContext.Current.CancellationToken);
        Assert.True(scope.IsSuccess); Assert.True(scope.Value.ReadOnly);
        proxy.Related = false;
        Assert.True((await policy.AuthorizeAsync(Guid.NewGuid(), TestContext.Current.CancellationToken)).IsFailure);
        proxy.Related = true;
        proxy.Snapshot = proxy.Snapshot with { Permissions = [PermissionNames.DoctorPatientsViewOwn, PermissionNames.PrescriptionsViewOwn] };
        Assert.True((await history.AuthorizeOwnReadAsync(proxy.Practice.Id, PermissionNames.PrescriptionsViewOwn, Guid.NewGuid(), TestContext.Current.CancellationToken)).IsFailure);
    }
    [Fact]
    public void New_permissions_are_unique_append_only_and_have_deterministic_ids()
    {
        Assert.Equal(PermissionNames.DoctorPatientDefaults, PermissionNames.All.TakeLast(3));
        Assert.Equal(PermissionNames.All.Count, PermissionNames.All.Distinct(StringComparer.Ordinal).Count());
        var first = PermissionNames.All.Count - 2;
        Assert.Equal(Guid.Parse($"20000000-0000-0000-0000-{first:D12}"), SystemPermissionIds.For(PermissionNames.DoctorPatientsViewOwn));
    }
    [Fact]
    public async Task Paging_anchor_rejects_utc_window_underflow_and_unspecified_timestamps()
    {
        await using var provider = new ServiceCollection().AddWaslaApplication().BuildServiceProvider();
        var validator = provider.GetRequiredService<IValidator<DoctorPatientsQuery>>();
        var modern = new DateTime(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc);
        Assert.True((await validator.ValidateAsync(new(AsOfUtc: modern), TestContext.Current.CancellationToken)).IsValid);
        Assert.False((await validator.ValidateAsync(new(AsOfUtc: DateTime.SpecifyKind(DateTime.MinValue.AddDays(1), DateTimeKind.Utc)),
            TestContext.Current.CancellationToken)).IsValid);
        Assert.False((await validator.ValidateAsync(new(AsOfUtc: DateTime.SpecifyKind(modern, DateTimeKind.Unspecified)),
            TestContext.Current.CancellationToken)).IsValid);
    }
    private static (DoctorPatientRelationshipPolicy, DoctorOwnHistoricalReadPolicy, PolicyProxy) Setup(
        UserType type = UserType.Doctor, DoctorApprovalStatus status = DoctorApprovalStatus.Approved, bool active = true, bool firstLogin = false)
    {
        var store = DispatchProxy.Create<IWaslaDataStore, PolicyProxy>(); var proxy = (PolicyProxy)store;
        var user = ApplicationUser.Create(Guid.NewGuid(), "synthetic", "synthetic@example.test", null, "hash", type, firstLogin, DateTime.UtcNow).Value;
        if (!active) user.Deactivate();
        var doctor = Guid.NewGuid();
        proxy.Snapshot = new(user, [SystemRoleNames.Doctor], Grants, doctor, status, null, null, null, false);
        proxy.Practice = DoctorPractice.Create(Guid.NewGuid(), doctor, "عيادة", "Practice", 1, 1, 1, "Address", 30, 31, user.Id).Value;
        var current = DispatchProxy.Create<ICurrentUser, PolicyProxy>(); ((PolicyProxy)current).Snapshot = proxy.Snapshot;
        var reader = DispatchProxy.Create<IDoctorPatientReadService, PolicyProxy>(); ((PolicyProxy)reader).Parent = proxy;
        var policy = new DoctorPatientRelationshipPolicy(store, current, reader);
        return (policy, new(policy, reader), proxy);
    }
    public class PolicyProxy : DispatchProxy
    {
        public UserAccessSnapshot Snapshot { get; set; } = null!;
        public DoctorPractice Practice { get; set; } = null!;
        public bool Related { get; set; } = true;
        public PolicyProxy? Parent { get; set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
        {
            "get_IsAuthenticated" => true, "get_UserId" => (Guid?)Snapshot.User.Id,
            "get_Permissions" => Grants, "GetAccessSnapshotAsync" => Task.FromResult<UserAccessSnapshot?>(Snapshot),
            "FindDoctorPracticeAsync" => Task.FromResult<DoctorPractice?>(Practice),
            "HasAttendanceAsync" => Task.FromResult((Parent ?? this).Related),
            "PracticeScopeAsync" => Task.FromResult<DoctorPatientPracticeScope?>(new((Parent ?? this).Practice.DoctorId, (Parent ?? this).Practice.IsActive)),
            _ => throw new NotSupportedException(targetMethod?.Name)
        };
    }
}

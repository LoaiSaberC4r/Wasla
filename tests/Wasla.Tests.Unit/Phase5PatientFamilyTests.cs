using System.Reflection;
using BuildingBlock.Application.Time;
using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Domain.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Application;
using Wasla.Application.Features.Patients;
using Wasla.Application.Features.Registration;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Families;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations;

namespace Wasla.Tests.Unit;

public sealed class Phase5PatientDomainTests
{
    private static readonly DateOnly Today = new(2026, 9, 8);

    [Fact]
    public void Patient_RequiresValidNameDobAndGender()
    {
        Assert.True(Create(nameAr: " ").IsFailure);
        Assert.True(Create(dateOfBirth: Today.AddDays(1)).IsFailure);
        Assert.True(Patient.Create(Guid.NewGuid(), "مريض", null, default, Gender.Male,
            null, null, null, null, null, Today).IsFailure);
        Assert.True(Create(gender: (Gender)999).IsFailure);
        Assert.True(Create(dateOfBirth: Today).IsSuccess);
    }

    [Fact]
    public void Patient_NormalizesProfileAndDerivesAgeAroundBirthday()
    {
        var patient = Create(
            nameAr: "  يوسف أحمد  ",
            dateOfBirth: new DateOnly(2010, 9, 8),
            phone: " 01012345678 ",
            email: " TEST@EXAMPLE.COM ").Value;

        Assert.Equal("يوسف أحمد", patient.NameAr);
        Assert.Equal("01012345678", patient.PhoneNumber);
        Assert.Equal("test@example.com", patient.Email);
        Assert.Equal(15, patient.GetAge(new DateOnly(2026, 9, 7)));
        Assert.Equal(16, patient.GetAge(new DateOnly(2026, 9, 8)));
        Assert.DoesNotContain(patient.GetType().GetProperties(), property => property.Name == "Age");
    }

    [Fact]
    public void Patients_CanSharePhoneAndCanExistWithoutAccount()
    {
        var first = Create(phone: "01012345678").Value;
        var second = Create(phone: "01012345678").Value;
        var withoutPhone = Create(phone: null).Value;

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(first.PhoneNumber, second.PhoneNumber);
        Assert.Null(withoutPhone.PhoneNumber);
        Assert.Null(typeof(Patient).GetProperty("ApplicationUserId"));
        Assert.Null(typeof(Patient).GetProperty("ApplicationUser"));
    }

    [Fact]
    public void PatientAccountLink_IsExplicitAndValidatesBothIdentities()
    {
        var patientId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var link = PatientAccountLink.Create(Guid.NewGuid(), userId, patientId,
            PatientAccountLinkSource.SelfRegistration, DateTime.UtcNow);

        Assert.True(link.IsSuccess);
        Assert.Equal(userId, link.Value.ApplicationUserId);
        Assert.Equal(patientId, link.Value.PatientId);
        Assert.True(PatientAccountLink.Create(Guid.NewGuid(), Guid.Empty, patientId,
            PatientAccountLinkSource.SelfRegistration, DateTime.UtcNow).IsFailure);
    }

    private static BuildingBlock.Domain.Results.Result<Patient> Create(
        string nameAr = "مريض",
        DateOnly? dateOfBirth = null,
        Gender gender = Gender.Male,
        string? phone = null,
        string? email = null)
        => Patient.Create(Guid.NewGuid(), nameAr, null, dateOfBirth ?? new DateOnly(2000, 1, 1),
            gender, phone, email, null, null, null, Today);
}

public sealed class Phase5FamilyDomainTests
{
    private static readonly DateTime Now = new(2026, 9, 8, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Family_AllowsManyChildrenAndGuardiansButOnlyOneFatherAndMother()
    {
        var family = Family.Create(Guid.NewGuid(), Guid.NewGuid()).Value;
        Assert.Equal(FamilyStatus.Active, family.Status);
        Assert.True(Add(family, FamilyMemberRole.Father).IsSuccess);
        Assert.True(Add(family, FamilyMemberRole.Mother).IsSuccess);
        Assert.True(Add(family, FamilyMemberRole.Child).IsSuccess);
        Assert.True(Add(family, FamilyMemberRole.Child).IsSuccess);
        Assert.True(Add(family, FamilyMemberRole.Guardian).IsSuccess);
        Assert.True(Add(family, FamilyMemberRole.Guardian).IsSuccess);
        Assert.True(Add(family, FamilyMemberRole.LegalGuardian).IsSuccess);
        Assert.True(Add(family, FamilyMemberRole.Father).IsFailure);
        Assert.True(Add(family, FamilyMemberRole.Mother).IsFailure);
    }

    [Fact]
    public void Family_RejectsDuplicatePatient()
    {
        var family = Family.Create(Guid.NewGuid(), Guid.NewGuid()).Value;
        var patientId = Guid.NewGuid();
        Assert.True(Add(family, FamilyMemberRole.Child, patientId).IsSuccess);
        var duplicate = Add(family, FamilyMemberRole.Guardian, patientId);
        Assert.True(duplicate.IsFailure);
        Assert.Equal("Family.AlreadyMember", duplicate.Errors.Single().Code);
    }

    [Fact]
    public void Request_FollowsApprovedStateMachineAndIncrementsRevision()
    {
        var request = CreateRequest();
        Assert.Equal(FamilyRelationshipRequestStatus.Pending, request.Status);
        Assert.Equal(1, request.CurrentRevisionNumber);
        Assert.True(request.RequestModification(" clearer evidence ", Guid.NewGuid(), Now).IsSuccess);
        Assert.Equal(FamilyRelationshipRequestStatus.ModificationRequested, request.Status);
        Assert.True(request.Resubmit(Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
        Assert.Equal(FamilyRelationshipRequestStatus.Pending, request.Status);
        Assert.Equal(2, request.CurrentRevisionNumber);
        Assert.True(request.Approve(Guid.NewGuid(), Now.AddMinutes(2)).IsSuccess);
        Assert.True(request.Reject("cannot change final", Guid.NewGuid(), Now.AddMinutes(3)).IsFailure);
        Assert.True(request.RequestModification("cannot change final", Guid.NewGuid(), Now.AddMinutes(3)).IsFailure);
    }

    [Fact]
    public void Request_RejectsWithoutReasonAndFinalRejectedCannotTransition()
    {
        var request = CreateRequest();
        Assert.True(request.Reject(" ", Guid.NewGuid(), Now).IsFailure);
        Assert.True(request.Reject("invalid evidence", Guid.NewGuid(), Now).IsSuccess);
        Assert.True(request.Approve(Guid.NewGuid(), Now.AddMinutes(1)).IsFailure);
        Assert.True(request.Resubmit(Guid.NewGuid(), Now.AddMinutes(1)).IsFailure);
    }

    [Fact]
    public void RequestHistory_IsAppendOnlyByContract()
    {
        var history = new FamilyRelationshipRequestHistory(Guid.NewGuid(), Guid.NewGuid(), 1, null,
            FamilyRelationshipRequestStatus.Pending, FamilyRelationshipRequestAction.Submitted, null, Guid.NewGuid(), Now);

        Assert.All(typeof(FamilyRelationshipRequestHistory).GetProperties(), property => Assert.False(property.SetMethod?.IsPublic == true));
        Assert.Equal(FamilyRelationshipRequestAction.Submitted, history.Action);
    }

    [Fact]
    public void GuardianMedicalAccess_StopsAtSixteenWithoutRemovingFamilyRelationship()
    {
        var family = Family.Create(Guid.NewGuid(), Guid.NewGuid()).Value;
        var father = Add(family, FamilyMemberRole.Father).Value;
        var childPatient = Patient.Create(Guid.NewGuid(), "طفل", null, new DateOnly(2010, 9, 8), Gender.Male,
            null, null, null, null, null, new DateOnly(2026, 9, 8)).Value;
        var child = Add(family, FamilyMemberRole.Child, childPatient.Id).Value;
        var policy = new PatientAccessPolicy();

        Assert.True(policy.CanAccessDependentMedicalData(father, child, childPatient, new DateOnly(2026, 9, 7)));
        Assert.False(policy.CanAccessDependentMedicalData(father, child, childPatient, new DateOnly(2026, 9, 8)));
        Assert.True(father.IsActive);
        Assert.True(child.IsActive);
        Assert.Equal(father.FamilyId, child.FamilyId);
    }

    [Fact]
    public void ContactOnlyFather_GrantsNoFamilyOrMedicalAccess()
    {
        var contact = PatientContact.Create(Guid.NewGuid(), Guid.NewGuid(), "أحمد", null, "0101",
            PatientContactRelationshipType.Father, null, true).Value;

        Assert.Equal(PatientContactRelationshipType.Father, contact.RelationshipType);
        Assert.False(typeof(FamilyMember).IsAssignableFrom(contact.GetType()));
        Assert.Null(contact.GetType().GetProperty("FamilyId"));
    }

    private static FamilyRelationshipRequest CreateRequest()
        => FamilyRelationshipRequest.Create(Guid.NewGuid(), FamilyRelationshipRequestType.CreateFamily, null,
            Guid.NewGuid(), Guid.NewGuid(), FamilyMemberRole.Father, FamilyMemberRole.Child, Guid.NewGuid(), Now).Value;

    private static BuildingBlock.Domain.Results.Result<FamilyMember> Add(Family family, FamilyMemberRole role, Guid? patientId = null)
        => family.AddMember(Guid.NewGuid(), patientId ?? Guid.NewGuid(), role, Now, Guid.NewGuid(), Guid.NewGuid());
}

public sealed class Phase5PatientFamilyValidationTests
{
    private static ServiceProvider Provider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDateTimeProvider>(new FixedClock());
        services.AddWaslaApplication();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ReceptionPatient_NoPhoneRequiresPrimaryContact()
    {
        await using var provider = Provider();
        var validator = provider.GetRequiredService<IValidator<CreateReceptionPatientCommand>>();
        var invalid = new CreateReceptionPatientCommand("طفل", null, new DateOnly(2020, 1, 1), Gender.Male,
            null, null, null, null);
        var valid = invalid with { PrimaryContact = new PatientContactInput("ولي الأمر", null, "0101", PatientContactRelationshipType.Guardian, null, true) };

        Assert.False((await validator.ValidateAsync(invalid, TestContext.Current.CancellationToken)).IsValid);
        Assert.True((await validator.ValidateAsync(valid, TestContext.Current.CancellationToken)).IsValid);
    }

    [Fact]
    public async Task ReceptionPatient_RejectsFutureDobAndMissingGender()
    {
        await using var provider = Provider();
        var validator = provider.GetRequiredService<IValidator<CreateReceptionPatientCommand>>();
        var command = new CreateReceptionPatientCommand("مريض", null, new DateOnly(2026, 9, 9), default,
            "0101", null, null, null);
        var result = await validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.DateOfBirth));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.Gender));
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTime UtcNow => new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);
    }
}

public sealed class Phase5RegistrationApplicationTests
{
    [Fact]
    public async Task SelfRegistration_CreatesUserPatientLinkAndRole()
    {
        var store = DispatchProxy.Create<IWaslaDataStore, RegistrationDataStoreProxy>();
        var services = new ServiceCollection();
        services.AddSingleton(store);
        services.AddSingleton<IPasswordService, RegistrationPasswordService>();
        services.AddSingleton<IMediaService, UnusedMediaService>();
        services.AddSingleton<IDateTimeProvider, RegistrationClock>();
        services.AddWaslaApplication();
        await using var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IRequestHandler<RegisterPatientCommand, Result<RegisterPatientResponse>>>();
        var command = new RegisterPatientCommand("patient", "PATIENT@example.test", "01012345678", "Strong123", "Strong123",
            "مريض", null, new DateOnly(2000, 1, 1), Gender.Male, null, null, null);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);
        var proxy = (RegistrationDataStoreProxy)(object)store;

        Assert.True(result.IsSuccess);
        Assert.Contains(proxy.Added, item => item is ApplicationUser user && user.Id == result.Value.ApplicationUserId);
        Assert.Contains(proxy.Added, item => item is Patient patient && patient.Id == result.Value.PatientId &&
                                            patient.PhoneNumber == "01012345678" && patient.Email == "patient@example.test");
        Assert.Contains(proxy.Added, item => item is PatientAccountLink link &&
                                            link.ApplicationUserId == result.Value.ApplicationUserId &&
                                            link.PatientId == result.Value.PatientId &&
                                            link.Source == PatientAccountLinkSource.SelfRegistration);
        Assert.Contains(proxy.Added, item => item is UserRole role && role.ApplicationUserId == result.Value.ApplicationUserId &&
                                            role.RoleId == SystemRoleIds.Patient);
    }
}

public sealed class Phase5PersistenceContractTests
{
    [Fact]
    public void Model_EnforcesIdentityAndFamilyCardinalityWithoutUniquePhone()
    {
        var options = new DbContextOptionsBuilder<WaslaDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=WaslaPhase5Model;Trusted_Connection=True")
            .Options;
        using var context = new WaslaDbContext(options);
        var patient = context.Model.FindEntityType(typeof(Patient))!;
        var link = context.Model.FindEntityType(typeof(PatientAccountLink))!;
        var member = context.Model.FindEntityType(typeof(FamilyMember))!;

        Assert.Null(patient.FindProperty("ApplicationUserId"));
        Assert.True(patient.FindProperty(nameof(Patient.RowVersion))!.IsConcurrencyToken);
        Assert.False(patient.GetIndexes().Single(index => index.Properties.Single().Name == nameof(Patient.PhoneNumber)).IsUnique);
        Assert.Contains(link.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(PatientAccountLink.ApplicationUserId));
        Assert.Contains(link.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(PatientAccountLink.PatientId));
        Assert.Contains(member.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(FamilyMember.FamilyId), nameof(FamilyMember.Role)]) && index.GetFilter()!.Contains("[Role] IN (1, 2)", StringComparison.Ordinal));
        Assert.Contains(member.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(FamilyMember.PatientId) && index.GetFilter()!.Contains("[IsActive] = 1", StringComparison.Ordinal));
    }

    [Fact]
    public void Migration_BackfillsBeforeDroppingLegacyPatientLink()
    {
        var operations = Operations("Up");
        var addPhone = operations.FindIndex(operation => operation is AddColumnOperation column && column.Table == "Patients" && column.Name == "PhoneNumber");
        var createLinks = operations.FindIndex(operation => operation is CreateTableOperation table && table.Name == "PatientAccountLinks");
        var backfill = operations.FindIndex(operation => operation is SqlOperation sql && sql.Sql.Contains("INSERT INTO [PatientAccountLinks]", StringComparison.Ordinal));
        var dropLegacy = operations.FindIndex(operation => operation is DropColumnOperation column && column.Table == "Patients" && column.Name == "ApplicationUserId");

        Assert.True(addPhone >= 0 && createLinks > addPhone && backfill > createLinks && dropLegacy > backfill);
        var sql = ((SqlOperation)operations[backfill]).Sql;
        Assert.Contains("[p].[PhoneNumber] = [u].[PhoneNumber]", sql, StringComparison.Ordinal);
        Assert.Contains("THROW 51001", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_DownFailsRatherThanDiscardingPatientsWithoutLinks()
    {
        var operations = Operations("Down");
        var addLegacy = operations.FindIndex(operation => operation is AddColumnOperation column && column.Name == "ApplicationUserId");
        var restoreSql = operations.FindIndex(operation => operation is SqlOperation sql && sql.Sql.Contains("THROW 51002", StringComparison.Ordinal));
        var dropLinks = operations.FindIndex(operation => operation is DropTableOperation table && table.Name == "PatientAccountLinks");
        Assert.True(addLegacy >= 0 && restoreSql > addLegacy && dropLinks > restoreSql);
    }

    [Fact]
    public void ExistingPermissionIdsRemainStableAndNewPermissionsAreAppended()
    {
        string[] existing =
        [
            PermissionNames.DoctorsViewAll, PermissionNames.DoctorsViewDetails, PermissionNames.DoctorsApprove,
            PermissionNames.DoctorsReject, PermissionNames.DoctorsSuspend, PermissionNames.DoctorsReactivate,
            PermissionNames.RolesView, PermissionNames.PermissionsView, PermissionNames.RolePermissionsManage,
            PermissionNames.DoctorOnboardingViewOwn, PermissionNames.PatientProfileViewOwn,
            PermissionNames.SuperAdminsViewAll, PermissionNames.SuperAdminsViewDetails, PermissionNames.SuperAdminsCreate,
            PermissionNames.SuperAdminsUpdate, PermissionNames.SuperAdminsActivate, PermissionNames.SuperAdminsDeactivate,
            PermissionNames.SuperAdminsDelete, PermissionNames.SuperAdminsRestore, PermissionNames.SpecializationsView,
            PermissionNames.SpecializationsCreate, PermissionNames.SpecializationsUpdate, PermissionNames.SpecializationsActivate,
            PermissionNames.SpecializationsDeactivate, PermissionNames.SpecializationsDelete, PermissionNames.SpecializationsRestore,
            PermissionNames.DoctorSpecializationsViewOwn, PermissionNames.DoctorSpecializationsSubmitOwn,
            PermissionNames.DoctorSpecializationsResubmitOwn, PermissionNames.DoctorSpecializationRequestsViewAll,
            PermissionNames.DoctorSpecializationRequestsViewDetails, PermissionNames.DoctorSpecializationRequestsAdjust,
            PermissionNames.DoctorSpecializationRequestsApprove, PermissionNames.DoctorSpecializationRequestsReject,
            PermissionNames.DoctorSpecializationRequestsRequestModification, PermissionNames.DoctorPracticeLocationViewOwn,
            PermissionNames.DoctorPracticeLocationManageOwn
        ];

        Assert.Equal(existing, PermissionNames.All.Take(existing.Length));
        for (var i = 0; i < existing.Length; i++)
            Assert.Equal(Guid.Parse($"20000000-0000-0000-0000-{i + 1:D12}"), SystemPermissionIds.For(existing[i]));
        Assert.Equal(PermissionNames.PatientsSearchBasic, PermissionNames.All[existing.Length]);
    }

    private static List<MigrationOperation> Operations(string methodName)
    {
        var migration = new Phase5PatientFamilyFoundation();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        typeof(Phase5PatientFamilyFoundation).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return builder.Operations.ToList();
    }
}

public class RegistrationDataStoreProxy : DispatchProxy
{
    public List<object> Added { get; } = [];

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        => targetMethod?.Name switch
        {
            nameof(IWaslaDataStore.UserNameExistsAsync) => Task.FromResult(false),
            nameof(IWaslaDataStore.EmailExistsAsync) => Task.FromResult(false),
            nameof(IWaslaDataStore.FindRoleByNameAsync) => Task.FromResult<Role?>(
                Role.Create(SystemRoleIds.Patient, SystemRoleNames.Patient, true).Value),
            nameof(IWaslaDataStore.SaveChangesAsync) => Task.FromResult(1),
            nameof(IWaslaDataStore.Add) => Capture(args),
            _ => throw new NotSupportedException(targetMethod?.Name)
        };

    private object? Capture(object?[]? args)
    {
        Added.Add(args![0]!);
        return null;
    }
}

internal sealed class RegistrationPasswordService : IPasswordService
{
    public string Hash(string password) => "hash";
    public bool Verify(string password, string passwordHash) => true;
    public PasswordVerification VerifyDetailed(string password, string passwordHash) => new(true, false);
    public Task<string> HashAsync(string password, CancellationToken ct = default) => Task.FromResult("hash");
    public Task<bool> VerifyAsync(string password, string passwordHash, CancellationToken ct = default) => Task.FromResult(true);
    public bool IsStrongPassword(string password) => true;
}

internal sealed class UnusedMediaService : IMediaService
{
    public Task<StoredMedia> SaveAsync(MediaUpload upload, MediaStorageRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<StoredMedia>> SaveAsync(IEnumerable<MediaUpload> uploads, MediaStorageRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task DeleteAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
    public Task DeleteRangeAsync(IEnumerable<string> keys, CancellationToken ct = default) => throw new NotSupportedException();
}

internal sealed class RegistrationClock : IDateTimeProvider
{
    public DateTime UtcNow => new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);
}

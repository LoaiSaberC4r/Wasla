using Wasla.Domain.Doctors;
using Wasla.Domain.ReferenceData;

namespace Wasla.Tests.Unit;

public sealed class DoctorOnboardingDomainTests
{
    private static readonly Guid ActorId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void MedicalSpecialization_Create_TrimsNamesAndAllowsNullEnglishName()
    {
        var result = MedicalSpecialization.Create(Guid.NewGuid(), "  أمراض القلب  ", null, null, null, 0, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal("أمراض القلب", result.Value.NameAr);
        Assert.Null(result.Value.NameEn);
        Assert.True(result.Value.IsActive);
        Assert.False(result.Value.IsDeleted);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MedicalSpecialization_Create_RejectsMissingArabicName(string nameAr)
        => Assert.True(MedicalSpecialization.Create(
            Guid.NewGuid(), nameAr, "Cardiology", null, null, 0, ActorId).IsFailure);

    [Fact]
    public void MedicalSpecialization_Create_RejectsNegativeSortOrder()
        => Assert.True(MedicalSpecialization.Create(
            Guid.NewGuid(), "القلب", "Cardiology", null, null, -1, ActorId).IsFailure);

    [Fact]
    public void MedicalSpecialization_Lifecycle_RequiresDeactivationAndRestoreRemainsInactive()
    {
        var item = CreateSpecialization();

        Assert.True(item.SoftDelete(ActorId).IsFailure);
        Assert.True(item.Deactivate(ActorId).IsSuccess);
        Assert.True(item.Deactivate(ActorId).IsFailure);
        Assert.True(item.SoftDelete(ActorId).IsSuccess);
        Assert.True(item.IsDeleted);
        Assert.True(item.Restore(ActorId).IsSuccess);
        Assert.False(item.IsDeleted);
        Assert.False(item.IsActive);
        Assert.True(item.Activate(ActorId).IsSuccess);
        Assert.True(item.Activate(ActorId).IsFailure);
    }

    [Fact]
    public void DoctorSpecializationSet_AcceptsMultipleWithExactlyOnePrimary()
    {
        var result = DoctorSpecializationSet.Validate(
        [
            new(Guid.NewGuid(), true),
            new(Guid.NewGuid(), false)
        ]);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void DoctorSpecializationSet_RejectsEmptySet()
        => Assert.Equal(
            "DoctorSpecializations.NoSpecializations",
            DoctorSpecializationSet.Validate([]).Errors.Single().Code);

    [Fact]
    public void DoctorSpecializationSet_RejectsNoPrimary()
        => Assert.Equal(
            "DoctorSpecializations.PrimaryRequired",
            DoctorSpecializationSet.Validate([new(Guid.NewGuid(), false)]).Errors.Single().Code);

    [Fact]
    public void DoctorSpecializationSet_RejectsMultiplePrimary()
        => Assert.Equal(
            "DoctorSpecializations.MultiplePrimary",
            DoctorSpecializationSet.Validate([new(Guid.NewGuid(), true), new(Guid.NewGuid(), true)]).Errors.Single().Code);

    [Fact]
    public void DoctorSpecializationSet_RejectsDuplicateSpecialization()
    {
        var id = Guid.NewGuid();
        Assert.Equal(
            "DoctorSpecializations.DuplicateSpecialization",
            DoctorSpecializationSet.Validate([new(id, true), new(id, false)]).Errors.Single().Code);
    }

    [Fact]
    public void SpecializationRequest_ModificationAndResubmit_CreateNextRevisionState()
    {
        var request = DoctorSpecializationRequest.Create(
            Guid.NewGuid(), Guid.NewGuid(), DoctorSpecializationRequestType.Initial, ActorId, DateTime.UtcNow).Value;

        Assert.True(request.RequestModification(ActorId, DateTime.UtcNow).IsSuccess);
        Assert.Equal(DoctorSpecializationRequestStatus.ModificationRequested, request.Status);
        Assert.True(request.Resubmit(ActorId, DateTime.UtcNow).IsSuccess);
        Assert.Equal(DoctorSpecializationRequestStatus.PendingReview, request.Status);
        Assert.Equal(2, request.CurrentRevisionNumber);
    }

    [Fact]
    public void SpecializationRequest_ApproveOnlyAllowsPendingReview()
    {
        var request = DoctorSpecializationRequest.Create(
            Guid.NewGuid(), Guid.NewGuid(), DoctorSpecializationRequestType.Change, ActorId, DateTime.UtcNow).Value;
        Assert.True(request.Approve(ActorId, DateTime.UtcNow).IsSuccess);
        Assert.True(request.Approve(ActorId, DateTime.UtcNow).IsFailure);
    }

    [Theory]
    [InlineData(-90.000001, 31.0, "DoctorPracticeLocation.InvalidLatitude")]
    [InlineData(90.000001, 31.0, "DoctorPracticeLocation.InvalidLatitude")]
    [InlineData(30.0, -180.000001, "DoctorPracticeLocation.InvalidLongitude")]
    [InlineData(30.0, 180.000001, "DoctorPracticeLocation.InvalidLongitude")]
    public void DoctorPracticeLocation_RejectsInvalidCoordinates(
        double latitude,
        double longitude,
        string code)
    {
        var result = DoctorPracticeLocation.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, 1001, 10010001, "15 شارع الاختبار", (decimal)latitude, (decimal)longitude);

        Assert.True(result.IsFailure);
        Assert.Equal(code, result.Errors.Single().Code);
    }

    private static MedicalSpecialization CreateSpecialization()
        => MedicalSpecialization.Create(
            Guid.NewGuid(), "أمراض القلب", "Cardiology", null, null, 10, ActorId).Value;
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Wasla.Domain.Doctors;
using Wasla.Domain.Practices;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Tests.Integration;

public sealed class DoctorPracticeOperationalPersistenceTests
{
    private static IModel CreateModel()
    {
        var options = new DbContextOptionsBuilder<WaslaDbContext>()
            .UseSqlServer("Server=localhost;Database=WaslaPracticeModel;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new WaslaDbContext(options);
        return context.Model;
    }

    [Fact]
    public void Operational_entities_have_concurrency_tokens_and_required_unique_constraints()
    {
        var model = CreateModel();

        AssertRowVersion<DoctorPractice>(model);
        AssertRowVersion<DoctorPracticeConfiguration>(model);
        AssertRowVersion<DoctorPracticeBranding>(model);
        AssertRowVersion<DoctorPracticeSchedulePeriod>(model);
        AssertRowVersion<DoctorPracticeScheduleException>(model);
        AssertRowVersion<DoctorPracticeSegment>(model);
        AssertRowVersion<DoctorPracticeVisitType>(model);
        AssertRowVersion<DoctorPracticeSegmentVisitTypePrice>(model);
        AssertRowVersion<ReceptionPracticeAssignment>(model);
        AssertRowVersion<DoctorQualification>(model);

        AssertUnique<DoctorPracticeConfiguration>(model, nameof(DoctorPracticeConfiguration.DoctorPracticeId));
        AssertUnique<DoctorPracticeBranding>(model, nameof(DoctorPracticeBranding.DoctorPracticeId));
        AssertUnique<DoctorPracticeVisitType>(
            model, nameof(DoctorPracticeVisitType.DoctorPracticeId), nameof(DoctorPracticeVisitType.Type));
        AssertUnique<DoctorPracticeSegmentVisitTypePrice>(
            model,
            nameof(DoctorPracticeSegmentVisitTypePrice.DoctorPracticeId),
            nameof(DoctorPracticeSegmentVisitTypePrice.SegmentId),
            nameof(DoctorPracticeSegmentVisitTypePrice.VisitTypeId));
        AssertUnique<ReceptionPracticeAssignment>(
            model,
            nameof(ReceptionPracticeAssignment.ReceptionId),
            nameof(ReceptionPracticeAssignment.DoctorPracticeId));
    }

    [Fact]
    public void Doctor_index_allows_many_practices_while_legacy_onboarding_remains_single()
    {
        var practice = CreateModel().FindEntityType(typeof(DoctorPractice))!;

        Assert.Contains(practice.GetIndexes(), index =>
            !index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(DoctorPractice.DoctorId)]));
        Assert.Contains(practice.GetIndexes(), index =>
            index.IsUnique && index.GetFilter() == "[IsLegacyOnboarding] = 1" &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(DoctorPractice.DoctorId)]));
    }

    [Fact]
    public void Public_discovery_fields_are_persisted_and_indexed_without_visit_type_duration()
    {
        var model = CreateModel();
        var doctor = model.FindEntityType(typeof(Doctor))!;
        var practice = model.FindEntityType(typeof(DoctorPractice))!;
        var qualification = model.FindEntityType(typeof(DoctorQualification))!;
        var visitType = model.FindEntityType(typeof(DoctorPracticeVisitType))!;

        Assert.NotNull(doctor.FindProperty(nameof(Doctor.NormalizedNameAr)));
        Assert.NotNull(doctor.FindProperty(nameof(Doctor.NormalizedNameEn)));
        Assert.NotNull(doctor.FindProperty(nameof(Doctor.Bio)));
        Assert.Contains(doctor.GetIndexes(), index => index.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(Doctor.NormalizedNameAr)]));
        Assert.Contains(doctor.GetIndexes(), index => index.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(Doctor.NormalizedNameEn)]));
        Assert.Contains(practice.GetIndexes(), index => index.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(DoctorPractice.DoctorId), nameof(DoctorPractice.IsActive)]));
        Assert.Contains(qualification.GetIndexes(), index => index.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(DoctorQualification.DoctorId), nameof(DoctorQualification.DisplayOrder)]));
        Assert.Null(visitType.FindProperty("DurationMinutes"));
    }

    private static void AssertRowVersion<TEntity>(IModel model)
    {
        var property = model.FindEntityType(typeof(TEntity))!.FindProperty("RowVersion")!;
        Assert.True(property.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
    }

    private static void AssertUnique<TEntity>(IModel model, params string[] properties)
    {
        var entity = model.FindEntityType(typeof(TEntity))!;
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(properties));
    }
}

using System.Globalization;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Practices;

namespace Wasla.Tests.Unit;

public sealed class DoctorPracticeOperationalDomainTests
{
    private static readonly Guid DoctorId = Guid.Parse("8c301d8a-bf4d-4fd5-bb79-48a8b4dd01c4");
    private static readonly Guid ActorId = Guid.Parse("c2869055-c06c-4c19-a9d7-5c86384e46f5");

    [Fact]
    public void Doctor_can_own_multiple_inactive_practices()
    {
        var cairo = CreatePractice("عيادة القاهرة");
        var shebin = CreatePractice("عيادة شبين");

        Assert.Equal(DoctorId, cairo.DoctorId);
        Assert.Equal(DoctorId, shebin.DoctorId);
        Assert.NotEqual(cairo.Id, shebin.Id);
        Assert.False(cairo.IsActive);
        Assert.False(shebin.IsActive);
    }

    [Fact]
    public void Activation_requires_logo_but_not_schedule()
    {
        var practice = CreatePractice("عيادة القاهرة");

        var withoutLogo = practice.Activate(hasConfiguration: true, hasBranding: true, hasLogo: false, ActorId);
        var withLogo = practice.Activate(hasConfiguration: true, hasBranding: true, hasLogo: true, ActorId);

        Assert.True(withoutLogo.IsFailure);
        Assert.Equal("DoctorPractice.ActivationRequirementsNotMet", withoutLogo.Errors.Single().Code);
        Assert.True(withLogo.IsSuccess);
        Assert.True(practice.IsActive);
    }

    [Fact]
    public void Online_booking_walk_in_and_lateness_are_independent_rules()
    {
        var configuration = DoctorPracticeConfiguration.CreateDefault(
            Guid.NewGuid(), Guid.NewGuid(), ActorId).Value;

        var updated = configuration.Update(
            allowOnlineBooking: false,
            allowWalkIn: true,
            defaultSlotDurationMinutes: 20,
            checkInGracePeriodMinutes: 15,
            patientSelfCancellationCutoffMinutes: 120,
            maximumDailyPatients: 12,
            maximumTicketCallAttempts: 3,
            timeZoneId: "UTC",
            ActorId);

        Assert.True(updated.IsSuccess);
        Assert.False(configuration.AllowOnlineBooking);
        Assert.True(configuration.AllowWalkIn);
        Assert.False(configuration.IsLate(Parse("2026-09-26T16:00:00Z"),
            Parse("2026-09-26T16:15:00Z")));
        Assert.True(configuration.IsLate(Parse("2026-09-26T16:00:00Z"),
            Parse("2026-09-26T16:15:01Z")));
        Assert.Equal(12, configuration.EffectiveDailyCapacity(20));
    }

    [Fact]
    public void Schedule_accepts_multiple_periods_and_detects_overlap()
    {
        var practiceId = Guid.NewGuid();
        var morning = DoctorPracticeSchedulePeriod.Create(
            Guid.NewGuid(), practiceId, DayOfWeek.Saturday,
            new TimeOnly(10, 0), new TimeOnly(14, 0), 20, ActorId).Value;

        Assert.False(morning.Overlaps(new TimeOnly(17, 0), new TimeOnly(21, 0)));
        Assert.True(morning.Overlaps(new TimeOnly(13, 0), new TimeOnly(17, 0)));
    }

    [Fact]
    public void Date_exception_overrides_recurring_schedule_and_blocked_ranges_are_subtracted()
    {
        var practiceId = Guid.NewGuid();
        var recurring = DoctorPracticeSchedulePeriod.Create(
            Guid.NewGuid(), practiceId, DayOfWeek.Saturday,
            new TimeOnly(10, 0), new TimeOnly(14, 0), 20, ActorId).Value;
        var date = new DateOnly(2026, 9, 26);
        var custom = DoctorPracticeScheduleException.Create(
            Guid.NewGuid(), practiceId, date, DoctorPracticeScheduleExceptionType.CustomWorkingHours,
            new TimeOnly(12, 0), new TimeOnly(16, 0), 30, ActorId).Value;
        var blocked = DoctorPracticeScheduleException.Create(
            Guid.NewGuid(), practiceId, date, DoctorPracticeScheduleExceptionType.BlockedTimeRange,
            new TimeOnly(13, 0), new TimeOnly(14, 0), null, ActorId).Value;

        var effective = DoctorPracticeAvailabilityCalculator.GetEffectiveWorkingPeriods(
            date, [recurring], [custom, blocked]);

        Assert.Equal(2, effective.Count);
        Assert.Equal(new PracticeWorkingPeriod(new TimeOnly(12, 0), new TimeOnly(13, 0), 30), effective[0]);
        Assert.Equal(new PracticeWorkingPeriod(new TimeOnly(14, 0), new TimeOnly(16, 0), 30), effective[1]);
    }

    [Fact]
    public void Normal_segment_is_protected_and_reserved_capacity_releases_at_cutoff()
    {
        var practiceId = Guid.NewGuid();
        var normal = DoctorPracticeSegment.CreateDefault(Guid.NewGuid(), practiceId, ActorId).Value;
        var vip = DoctorPracticeSegment.Create(
            Guid.NewGuid(), practiceId, "مميز", "VIP", 10, 5, 60, actorId: ActorId).Value;
        var workingDate = new DateOnly(2026, 9, 26);
        PracticeWorkingPeriod[] periods =
            [new(new TimeOnly(10, 0), new TimeOnly(20, 0), 20)];

        Assert.True(normal.Deactivate(ActorId).IsFailure);
        Assert.Equal(4, vip.ProtectedCapacity(
            1, workingDate, periods, Parse("2026-09-26T18:00:00Z"), TimeZoneInfo.Utc));
        Assert.Equal(0, vip.ProtectedCapacity(
            1, workingDate, periods, Parse("2026-09-26T19:00:00Z"), TimeZoneInfo.Utc));
    }

    [Fact]
    public void Queue_priority_visit_type_and_price_are_separate_concepts()
    {
        var earlierNormal = new PracticeQueueEntry(0, Parse("2026-09-26T10:00:00Z"));
        var laterVip = new PracticeQueueEntry(10, Parse("2026-09-26T10:10:00Z"));
        var ordered = new[] { earlierNormal, laterVip }.Order(DoctorPracticeQueueComparer.Instance).ToArray();
        var practiceId = Guid.NewGuid();
        var segment = DoctorPracticeSegment.CreateDefault(Guid.NewGuid(), practiceId, ActorId).Value;
        var visitType = DoctorPracticeVisitType.CreateDefault(
            Guid.NewGuid(), practiceId, DoctorPracticeVisitTypeCode.FollowUp, ActorId).Value;
        var price = DoctorPracticeSegmentVisitTypePrice.Create(
            Guid.NewGuid(), practiceId, segment.Id, visitType.Id, 300m, ActorId).Value;

        Assert.Equal(laterVip, ordered[0]);
        Assert.Equal(0, segment.Priority);
        Assert.Equal(DoctorPracticeVisitTypeCode.FollowUp, visitType.Type);
        Assert.Equal(300m, price.Price);
    }

    [Fact]
    public void Same_reception_profile_can_have_assignments_to_multiple_practices()
    {
        var reception = Reception.Create(
            Guid.NewGuid(), Guid.NewGuid(), DoctorId, "سارة", "Sara", ActorId).Value;
        var cairo = ReceptionPracticeAssignment.Create(
            Guid.NewGuid(), reception.Id, Guid.NewGuid(), ActorId).Value;
        var shebin = ReceptionPracticeAssignment.Create(
            Guid.NewGuid(), reception.Id, Guid.NewGuid(), ActorId).Value;

        Assert.Equal(reception.Id, cairo.ReceptionId);
        Assert.Equal(reception.Id, shebin.ReceptionId);
        Assert.NotEqual(cairo.DoctorPracticeId, shebin.DoctorPracticeId);
    }

    [Fact]
    public void Visit_types_do_not_own_slot_duration_and_configuration_keeps_setup_default()
    {
        var practiceId = Guid.NewGuid();
        var configuration = DoctorPracticeConfiguration.CreateDefault(
            Guid.NewGuid(), practiceId, ActorId).Value;
        var newConsultation = DoctorPracticeVisitType.CreateDefault(
            Guid.NewGuid(), practiceId, DoctorPracticeVisitTypeCode.NewConsultation, ActorId).Value;
        var followUp = DoctorPracticeVisitType.CreateDefault(
            Guid.NewGuid(), practiceId, DoctorPracticeVisitTypeCode.FollowUp, ActorId).Value;

        Assert.Null(typeof(DoctorPracticeVisitType).GetProperty("DurationMinutes"));
        Assert.Equal(20, configuration.DefaultSlotDurationMinutes);
        Assert.Equal("كشف جديد", newConsultation.NameAr);
        Assert.Equal("متابعة", followUp.NameAr);
    }

    [Fact]
    public void Schedule_period_duration_is_the_only_source_of_slot_boundaries()
    {
        var date = new DateOnly(2026, 9, 26);
        var period = DoctorPracticeSchedulePeriod.Create(
            Guid.NewGuid(), Guid.NewGuid(), DayOfWeek.Saturday,
            new TimeOnly(17, 0), new TimeOnly(20, 0), 20, ActorId).Value;

        var slots = DoctorPracticeAvailabilityCalculator.CalculateAvailableSlotStarts(
            date, [period], []);

        Assert.Equal(
            [
                new TimeOnly(17, 0), new TimeOnly(17, 20), new TimeOnly(17, 40),
                new TimeOnly(18, 0), new TimeOnly(18, 20), new TimeOnly(18, 40),
                new TimeOnly(19, 0), new TimeOnly(19, 20), new TimeOnly(19, 40)
            ],
            slots);
    }

    [Fact]
    public void Day_off_custom_hours_and_blocked_ranges_apply_to_the_original_slot_grid()
    {
        var practiceId = Guid.NewGuid();
        var date = new DateOnly(2026, 9, 26);
        var recurring = DoctorPracticeSchedulePeriod.Create(
            Guid.NewGuid(), practiceId, DayOfWeek.Saturday,
            new TimeOnly(10, 0), new TimeOnly(12, 0), 20, ActorId).Value;
        var custom = DoctorPracticeScheduleException.Create(
            Guid.NewGuid(), practiceId, date, DoctorPracticeScheduleExceptionType.CustomWorkingHours,
            new TimeOnly(17, 0), new TimeOnly(18, 20), 20, ActorId).Value;
        var blocked = DoctorPracticeScheduleException.Create(
            Guid.NewGuid(), practiceId, date, DoctorPracticeScheduleExceptionType.BlockedTimeRange,
            new TimeOnly(17, 25), new TimeOnly(17, 45), null, ActorId).Value;
        var dayOff = DoctorPracticeScheduleException.Create(
            Guid.NewGuid(), practiceId, date, DoctorPracticeScheduleExceptionType.DayOff,
            null, null, null, ActorId).Value;

        var customSlots = DoctorPracticeAvailabilityCalculator.CalculateAvailableSlotStarts(
            date, [recurring], [custom, blocked]);
        var noSlots = DoctorPracticeAvailabilityCalculator.CalculateAvailableSlotStarts(
            date, [recurring], [custom, dayOff]);

        Assert.Equal([new TimeOnly(17, 0), new TimeOnly(18, 0)], customSlots);
        Assert.Empty(noSlots);
    }

    [Fact]
    public void Arabic_name_normalization_is_searchable_without_collapsing_taa_marbuta()
    {
        Assert.Equal("احمد", DoctorNameNormalizer.NormalizeArabic("  أَحْـمَد  "));
        Assert.Equal("علي", DoctorNameNormalizer.NormalizeArabic("على"));
        Assert.Equal("منة", DoctorNameNormalizer.NormalizeArabic("منة"));
        Assert.NotEqual(
            DoctorNameNormalizer.NormalizeArabic("منة"),
            DoctorNameNormalizer.NormalizeArabic("منه"));
        Assert.Equal("sara adel", DoctorNameNormalizer.NormalizeEnglish("  SARA   Adel "));
    }

    [Fact]
    public void Doctor_bio_is_plain_text_and_qualification_has_bilingual_ordered_content()
    {
        var doctor = Doctor.Create(
            DoctorId,
            Guid.NewGuid(),
            "أحمد علي",
            "Ahmed Ali",
            new DateOnly(1985, 1, 1),
            Gender.Male,
            null,
            "personal-front",
            "personal-back",
            "syndicate-front",
            null,
            new DateOnly(2026, 9, 15)).Value;
        var qualification = DoctorQualification.Create(
            Guid.NewGuid(), doctor.Id, "دكتوراه القلب", null, 3, ActorId).Value;

        Assert.True(doctor.UpdateBio("  استشاري أمراض القلب\nخبرة 15 سنة  ", ActorId).IsSuccess);
        Assert.Equal("استشاري أمراض القلب\nخبرة 15 سنة", doctor.Bio);
        Assert.True(doctor.UpdateBio("<b>unsafe</b>", ActorId).IsFailure);
        Assert.Equal("دكتوراه القلب", qualification.NameAr);
        Assert.Null(qualification.NameEn);
        Assert.Equal(3, qualification.DisplayOrder);
        Assert.True(qualification.Update("زمالة القلب", "Cardiology fellowship", 1, ActorId).IsSuccess);
        Assert.Equal(1, qualification.DisplayOrder);
        Assert.True(DoctorQualification.Create(
            Guid.NewGuid(), doctor.Id, " ", null, 0, ActorId).IsFailure);
    }

    private static DoctorPractice CreatePractice(string nameAr)
        => DoctorPractice.Create(
            Guid.NewGuid(), DoctorId, nameAr, null, 1, 1001, 10010001,
            "15 شارع الاختبار", 30m, 31m, ActorId).Value;

    private static DateTimeOffset Parse(string value)
        => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
}

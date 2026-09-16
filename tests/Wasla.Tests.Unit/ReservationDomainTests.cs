using Wasla.Domain.Reservations;
using Wasla.Domain.Security;

namespace Wasla.Tests.Unit;

public sealed class ReservationDomainTests
{
    private static readonly DateTime OccurredOnUtc = new(2026, 9, 16, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_StartsActiveAndSnapshotsAppointmentCommercialDataAndHistory()
    {
        var reservation = CreateReservation();

        Assert.Equal(ReservationStatus.Active, reservation.Status);
        Assert.Equal("WSL-R-ABC234", reservation.ReservationReference);
        Assert.Equal(350m, reservation.PriceSnapshot);
        Assert.Equal(20, reservation.SlotDurationMinutesSnapshot);
        Assert.Equal("Normal", reservation.SegmentNameEnSnapshot);
        Assert.Equal("NewConsultation", reservation.VisitTypeCodeSnapshot);
        var created = Assert.Single(reservation.History);
        Assert.Equal(ReservationHistoryEventType.Created, created.EventType);
        Assert.Equal(ReservationStatus.Active, created.ToStatus);
    }

    [Fact]
    public void Cancel_TransitionsOnlyActiveAndAppendsImmutableHistory()
    {
        var reservation = CreateReservation();

        var result = reservation.Cancel(
            ReservationCancellationInitiator.Patient,
            ReservationActionInitiator.Patient,
            Guid.NewGuid(),
            ReservationCancellationReasons.PatientChangedPlans,
            null,
            OccurredOnUtc.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(2, reservation.History.Count);
        Assert.Equal(ReservationHistoryEventType.Cancelled, reservation.History.Last().EventType);
        Assert.True(reservation.Cancel(
            ReservationCancellationInitiator.Patient,
            ReservationActionInitiator.Patient,
            Guid.NewGuid(),
            ReservationCancellationReasons.PatientChangedPlans,
            null,
            OccurredOnUtc.AddHours(2)).IsFailure);
    }

    [Fact]
    public void PatientReschedule_PreservesIdentityCatalogAndPriceAndEnforcesLimit()
    {
        var reservation = CreateReservation();
        var id = reservation.Id;
        var reference = reservation.ReservationReference;
        var segment = reservation.SegmentId;
        var visitType = reservation.VisitTypeId;
        var price = reservation.PriceSnapshot;

        Assert.True(RescheduleAsPatient(reservation, 1).IsSuccess);
        Assert.True(RescheduleAsPatient(reservation, 2).IsSuccess);
        Assert.True(RescheduleAsPatient(reservation, 3).IsFailure);

        Assert.Equal(id, reservation.Id);
        Assert.Equal(reference, reservation.ReservationReference);
        Assert.Equal(segment, reservation.SegmentId);
        Assert.Equal(visitType, reservation.VisitTypeId);
        Assert.Equal(price, reservation.PriceSnapshot);
        Assert.Equal(2, reservation.PatientInitiatedRescheduleCount);
        Assert.Equal(25, reservation.SlotDurationMinutesSnapshot);
    }

    [Fact]
    public void ProviderReschedule_RequiresConsentAndDoesNotConsumePatientAllowance()
    {
        var reservation = CreateReservation();
        var denied = reservation.Reschedule(
            OccurredOnUtc.AddDays(2),
            DateTime.SpecifyKind(new DateTime(2026, 9, 18, 18, 0, 0), DateTimeKind.Unspecified),
            new DateOnly(2026, 9, 18),
            30,
            ReservationActionInitiator.Doctor,
            Guid.NewGuid(),
            false,
            "Schedule changed",
            OccurredOnUtc.AddHours(1));

        Assert.True(denied.IsFailure);
        Assert.True(reservation.Reschedule(
            OccurredOnUtc.AddDays(2),
            DateTime.SpecifyKind(new DateTime(2026, 9, 18, 18, 0, 0), DateTimeKind.Unspecified),
            new DateOnly(2026, 9, 18),
            30,
            ReservationActionInitiator.Doctor,
            Guid.NewGuid(),
            true,
            "Schedule changed",
            OccurredOnUtc.AddHours(1)).IsSuccess);
        Assert.Equal(0, reservation.PatientInitiatedRescheduleCount);
        Assert.True(reservation.History.Last().PatientConsentConfirmed);
    }

    [Fact]
    public void NoShow_Restore_ExpireAndConvert_RespectTransitionsAndCapacityStatus()
    {
        var noShow = CreateReservation();
        Assert.True(noShow.MarkNoShow(null, OccurredOnUtc.AddHours(3)).IsSuccess);
        Assert.False(noShow.ConsumesCapacity);
        Assert.True(noShow.RestoreFromNoShow(Guid.NewGuid(), OccurredOnUtc.AddHours(4)).IsSuccess);
        Assert.True(noShow.ConsumesCapacity);

        var expired = CreateReservation();
        Assert.True(expired.Expire(OccurredOnUtc.AddDays(1)).IsSuccess);
        Assert.False(expired.ConsumesCapacity);

        var converted = CreateReservation();
        Assert.True(converted.ConvertToTicket(Guid.NewGuid(), OccurredOnUtc.AddHours(2)).IsSuccess);
        Assert.True(converted.ConsumesCapacity);
        Assert.True(converted.Expire(OccurredOnUtc.AddDays(1)).IsFailure);
    }

    [Fact]
    public void BookingNoteHasNoMutationPathAndReferenceRemainsStable()
    {
        var reservation = CreateReservation();
        var reference = reservation.ReservationReference;

        Assert.Equal("operational note", reservation.BookingNote);
        Assert.True(RescheduleAsPatient(reservation, 1).IsSuccess);
        Assert.Equal("operational note", reservation.BookingNote);
        Assert.Equal(reference, reservation.ReservationReference);
        Assert.IsAssignableFrom<IReadOnlyCollection<ReservationHistory>>(reservation.History);
    }

    [Fact]
    public void ReservationPermissionsAreAppendOnlyAndReceptionGranularPermissionsAreDelegatable()
    {
        Assert.Equal(
            Guid.Parse("20000000-0000-0000-0000-000000000073"),
            SystemPermissionIds.For(PermissionNames.PracticeReservationsManage));
        Assert.Equal(
            Guid.Parse("20000000-0000-0000-0000-000000000078"),
            SystemPermissionIds.For(PermissionNames.DoctorProfileUpdateOwn));
        Assert.Contains(PermissionNames.PracticeReservationsView, PermissionNames.ReceptionAssignmentScoped);
        Assert.Contains(PermissionNames.PracticeReservationsCreate, PermissionNames.ReceptionAssignmentScoped);
        Assert.Contains(PermissionNames.PracticeReservationsCancel, PermissionNames.ReceptionAssignmentScoped);
        Assert.Contains(PermissionNames.PracticeReservationsReschedule, PermissionNames.ReceptionAssignmentScoped);
        Assert.Contains(PermissionNames.PracticeReservationsRestoreNoShow, PermissionNames.ReceptionAssignmentScoped);
        Assert.DoesNotContain(PermissionNames.PermissionsView, PermissionNames.ReceptionAssignmentScoped);
    }

    private static Reservation CreateReservation()
        => Reservation.Create(new ReservationCreationSnapshot(
            Guid.NewGuid(),
            "WSL-R-ABC234",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ReservationBookingSource.Patient,
            Guid.NewGuid(),
            null,
            new DateTime(2026, 9, 17, 16, 0, 0, DateTimeKind.Utc),
            DateTime.SpecifyKind(new DateTime(2026, 9, 17, 19, 0, 0), DateTimeKind.Unspecified),
            new DateOnly(2026, 9, 17),
            "Africa/Cairo",
            20,
            "عادي",
            "Normal",
            1,
            "NewConsultation",
            "كشف جديد",
            "New consultation",
            350m,
            "operational note",
            OccurredOnUtc)).Value;

    private static BuildingBlock.Domain.Results.Result RescheduleAsPatient(Reservation reservation, int days)
        => reservation.Reschedule(
            OccurredOnUtc.AddDays(days + 1),
            DateTime.SpecifyKind(new DateTime(2026, 9, 17 + days, 19, 0, 0), DateTimeKind.Unspecified),
            new DateOnly(2026, 9, 17 + days),
            25,
            ReservationActionInitiator.Patient,
            Guid.NewGuid(),
            true,
            null,
            OccurredOnUtc.AddHours(days));
}

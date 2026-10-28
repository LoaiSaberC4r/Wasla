using FluentValidation;
using Wasla.Domain.Clinical;
using Wasla.Domain.Medications;
using Wasla.Domain.Reservations;
using Wasla.Domain.Tickets;

namespace Wasla.Application.Features.DoctorPatients;

internal sealed class DoctorPatientsValidator : AbstractValidator<DoctorPatientsQuery>
{
    public DoctorPatientsValidator()
    {
        RuleFor(q => q.Search).MaximumLength(200);
        // The summary subtracts a rolling 30-day window from the paging anchor.
        RuleFor(q => q.AsOfUtc).Must(date => date is null || date.Value.Kind == DateTimeKind.Utc &&
            date >= DateTime.MinValue.AddDays(30) && date < DateTime.MaxValue);
        RuleFor(q => q.PracticeId).Must(id => id is null || id != Guid.Empty);
        RuleFor(q => q.PracticeScope).Must(s => s is "all" or "active" or "historicalOnly");
        RuleFor(q => q.PageNumber).InclusiveBetween(1, 1000000);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
        RuleFor(q => q.MinAttendanceCount).GreaterThan(0).When(q => q.MinAttendanceCount.HasValue);
        RuleFor(q => q.LastAttendanceFrom).Must(ValidDate);
        RuleFor(q => q.LastAttendanceTo).Must(ValidDate).Must((q, to) => to is null || q.LastAttendanceFrom is null || to >= q.LastAttendanceFrom);
    }
    internal static bool ValidDate(DateOnly? date) => date is null || date > DateOnly.MinValue && date < DateOnly.MaxValue;
}
internal sealed class DoctorMyPatientValidator : AbstractValidator<DoctorMyPatientQuery>
{
    public DoctorMyPatientValidator() => RuleFor(q => q.PatientId).NotEmpty();
}
internal sealed class DoctorPatientHistoryValidator : AbstractValidator<DoctorPatientHistoryQuery>
{
    public DoctorPatientHistoryValidator()
    {
        RuleFor(q => q.PatientId).NotEmpty();
        RuleFor(q => q.Resource).IsInEnum();
        RuleFor(q => q.PracticeId).Must(id => id is null || id != Guid.Empty);
        RuleFor(q => q.PageNumber).InclusiveBetween(1, 1000000);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
        RuleFor(q => q.FromDate).Must(DoctorPatientsValidator.ValidDate);
        RuleFor(q => q.ToDate).Must(DoctorPatientsValidator.ValidDate).Must((q, to) => to is null || q.FromDate is null || to >= q.FromDate);
        RuleFor(q => q.Status).MaximumLength(30).Must((q, s) => s is null || (q.Resource switch
        {
            DoctorPatientHistory.Attendances => Valid<TicketStatus>(s),
            DoctorPatientHistory.Prescriptions => Valid<PrescriptionVersionStatus>(s) && Enum.Parse<PrescriptionVersionStatus>(s, true) != PrescriptionVersionStatus.Draft,
            DoctorPatientHistory.Appointments => Valid<ReservationStatus>(s),
            DoctorPatientHistory.FollowUps => Valid<FollowUpEligibilityStatus>(s),
            _ => false
        }));
    }
    private static bool Valid<T>(string value) where T : struct, Enum => Enum.TryParse<T>(value, true, out var parsed) && Enum.IsDefined(parsed);
}
internal sealed class PrescriptionPdfValidator : AbstractValidator<PrescriptionPdfQuery>
{
    public PrescriptionPdfValidator()
    {
        RuleFor(q => q.PrescriptionId).NotEmpty(); RuleFor(q => q.VersionNumber).GreaterThan(0);
        RuleFor(q => q.Disposition).Must(s => s is "inline" or "attachment");
    }
}

using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Persistence;
using Wasla.Domain.Clinical;
using Wasla.Domain.Practices;
using Wasla.Domain.Reservations;
using Wasla.Domain.Tickets;

namespace Wasla.Application.Features.Clinical;

internal sealed class EncounterForUpdateSpecification : Specification<MedicalEncounter>
{
    public EncounterForUpdateSpecification(Guid id, bool byTicket = false)
    {
        AddCriteria(e => byTicket ? e.TicketId == id : e.Id == id);
        AddInclude(e => e.Diagnoses);
        AddInclude(e => e.Amendments);
        UseTracking();
    }
}

internal sealed class FollowUpWorkflow(IUnitOfWork<WaslaWritePersistence> unitOfWork, IDateTimeProvider clock)
{
    public Task<FollowUpEligibility?> FindAsync(Guid id, CancellationToken ct)
        => unitOfWork.WriteRepository<FollowUpEligibility>().GetByIdAsync(id, ct);

    public async Task<DateOnly> TodayAsync(Guid practiceId, CancellationToken ct)
    {
        var config = await unitOfWork.WriteRepository<DoctorPracticeConfiguration>()
            .GetByPropertyAsync(c => c.DoctorPracticeId == practiceId, ct);
        return TicketBusinessClock.CurrentBusinessDate(clock.UtcNow,
            config?.TimeZoneId ?? throw new InvalidOperationException("Practice configuration is required."));
    }

    public async Task<Result<FollowUpEligibility?>> ValidateSelectionAsync(Guid? id, DoctorPracticeVisitTypeCode type,
        Guid patientId, Guid practiceId, Guid doctorId, DateOnly visitDate, CancellationToken ct)
    {
        if ((type == DoctorPracticeVisitTypeCode.FollowUp) != id.HasValue)
            return Result<FollowUpEligibility?>.Fail(FollowUpErrors.InvalidVisitType);
        if (id is null) return Result<FollowUpEligibility?>.Ok(null);
        var eligibility = await FindAsync(id.Value, ct);
        if (eligibility is null) return Result<FollowUpEligibility?>.Fail(FollowUpErrors.NotFound);
        var scope = eligibility.ValidateScope(patientId, practiceId, doctorId);
        if (scope.IsFailure) return Result<FollowUpEligibility?>.Fail(scope.Errors);
        var usable = eligibility.ValidateAvailable(await TodayAsync(practiceId, ct), visitDate);
        return usable.IsFailure ? Result<FollowUpEligibility?>.Fail(usable.Errors) : Result<FollowUpEligibility?>.Ok(eligibility);
    }

    public async Task<Result<FollowUpEligibility?>> ValidateReservationAsync(Reservation reservation,
        DateOnly targetDate, bool restore, CancellationToken ct)
    {
        if (reservation.FollowUpEligibilityId is not { } id) return Result<FollowUpEligibility?>.Ok(null);
        var eligibility = await FindAsync(id, ct);
        if (eligibility is null) return Result<FollowUpEligibility?>.Fail(FollowUpErrors.NotFound);
        var scope = eligibility.ValidateScope(reservation.PatientId, reservation.DoctorPracticeId, reservation.DoctorId);
        if (scope.IsFailure) return Result<FollowUpEligibility?>.Fail(scope.Errors);
        var today = await TodayAsync(reservation.DoctorPracticeId, ct);
        var usable = restore ? eligibility.ValidateAvailable(today, targetDate)
            : eligibility.ValidateReservation(reservation.Id, today, targetDate);
        return usable.IsFailure ? Result<FollowUpEligibility?>.Fail(usable.Errors) : Result<FollowUpEligibility?>.Ok(eligibility);
    }

    public async Task<Result> RestoreTicketAsync(Ticket ticket, Guid actor, CancellationToken ct)
    {
        if (ticket.FollowUpEligibilityId is not { } id) return Result.Ok();
        var eligibility = await FindAsync(id, ct);
        if (eligibility is null) return Result.Fail(FollowUpErrors.NotFound);
        var scope = eligibility.ValidateScope(ticket.PatientId, ticket.DoctorPracticeId, ticket.DoctorId);
        if (scope.IsFailure) return scope;
        return eligibility.Reserve(null, ticket.Id, await TodayAsync(ticket.DoctorPracticeId, ct), ticket.BusinessDate, actor, clock.UtcNow);
    }
}

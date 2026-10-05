using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Persistence;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;
using Wasla.Domain.Clinical;
using Wasla.Domain.Practices;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Features.Medications;
using Wasla.Domain.Medications;

namespace Wasla.Application.Features.Tickets.CompleteTicket;

public sealed record CompleteTicketCommand(
    Guid PracticeId,
    Guid TicketId,
    string TicketRowVersion,
    string EncounterRowVersion,
    string IdempotencyKey,
    string? PrescriptionRowVersion = null)
    : ICommand<TicketDetailsResponse>, ITransactionalCommand<WaslaWritePersistence>;

internal sealed class CompleteTicketCommandValidator : AbstractValidator<CompleteTicketCommand>
{
    public CompleteTicketCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.TicketId).NotEmpty();
        RuleFor(command => command.TicketRowVersion).Must(TicketRowVersion.IsValid);
        RuleFor(command => command.EncounterRowVersion).Must(TicketRowVersion.IsValid);
        RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}

internal sealed class CompleteTicketCommandHandler(
    TicketAccessService access,
    FollowUpWorkflow followUp,
    IUnitOfWork<WaslaWritePersistence> unitOfWork,
    ITicketQueueLock queueLock,
    ITicketQueueReader queueReader,
    IDateTimeProvider clock)
    : ICommandHandler<CompleteTicketCommand, TicketDetailsResponse>
{
    public async Task<Result<TicketDetailsResponse>> Handle(
        CompleteTicketCommand request,
        CancellationToken cancellationToken)
    {
        var actor = await access.AuthorizeDoctorAsync(
            request.PracticeId, PermissionNames.DoctorPracticeTicketsCompleteOwn, cancellationToken);
        if (actor.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(actor.Errors);
        }

        var clinicalAccess = await access.AuthorizeDoctorAsync(
            request.PracticeId, PermissionNames.MedicalEncountersCompleteOwn, cancellationToken);
        if (clinicalAccess.IsFailure) return Result<TicketDetailsResponse>.Fail(clinicalAccess.Errors);

        var key = TicketIdempotency.ValidateKey(request.IdempotencyKey);
        if (key.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(key.Errors);
        }

        var ticket = await unitOfWork.WriteRepository<Ticket>().FirstOrDefaultAsync(
            new TicketByIdForUpdateSpecification(request.TicketId), cancellationToken);
        if (ticket is null || ticket.DoctorPracticeId != request.PracticeId)
        {
            return Result<TicketDetailsResponse>.Fail(TicketErrors.NotFound);
        }

        await queueLock.AcquirePracticeDayAsync(
            request.PracticeId, ticket.BusinessDate, cancellationToken);
        await queueLock.AcquireIdempotencyAsync(
            actor.Value.ApplicationUserId, "CompleteTicket", key.Value, cancellationToken);
        var nowUtc = EnsureUtc(clock.UtcNow);
        var idempotency = await TicketIdempotency.BeginAsync(
            unitOfWork.WriteRepository<TicketIdempotencyRecord>(),
            actor.Value.ApplicationUserId,
            "CompleteTicket",
            key.Value,
            TicketIdempotency.Fingerprint(request.PracticeId, request.TicketId, request.TicketRowVersion, request.EncounterRowVersion, request.PrescriptionRowVersion),
            nowUtc,
            cancellationToken);
        if (idempotency.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(idempotency.Errors);
        }

        if (idempotency.Value.ExistingTicketId is { } replayId)
        {
            return await DetailsAsync(replayId, cancellationToken);
        }

        if (!TicketRowVersion.Matches(ticket.RowVersion, request.TicketRowVersion))
        {
            return Result<TicketDetailsResponse>.Fail(TicketErrors.ConcurrencyConflict);
        }

        var encounter = await unitOfWork.WriteRepository<MedicalEncounter>().FirstOrDefaultAsync(
            new EncounterForUpdateSpecification(ticket.Id, byTicket: true), cancellationToken);
        if (encounter is null) return Result<TicketDetailsResponse>.Fail(ClinicalErrors.NotFound);
        if (!TicketRowVersion.Matches(encounter.RowVersion, request.EncounterRowVersion))
            return Result<TicketDetailsResponse>.Fail(ClinicalErrors.ConcurrencyConflict);
        var practice = await unitOfWork.WriteRepository<DoctorPractice>().GetByIdAsync(request.PracticeId, cancellationToken);
        if (practice?.DoctorId != ticket.DoctorId) return Result<TicketDetailsResponse>.Fail(ClinicalErrors.AccessDenied);
        await queueLock.AcquireIdempotencyAsync(Guid.Empty, "Phase14:EncounterPrescription", encounter.Id.ToString("N"), cancellationToken);
        var prescription = await unitOfWork.WriteRepository<Prescription>().FirstOrDefaultAsync(new PrescriptionForUpdate(encounter.Id, true), cancellationToken);
        if (prescription is not null && !TicketRowVersion.Matches(prescription.RowVersion, request.PrescriptionRowVersion ?? string.Empty) ||
            prescription is null && !string.IsNullOrWhiteSpace(request.PrescriptionRowVersion))
            return Result<TicketDetailsResponse>.Fail(MedicationErrors.Conflict("Prescription.ConcurrencyConflict"));
        var validationErrors = new List<Error>();
        if (prescription is not null) validationErrors.AddRange(prescription.FinalizationErrors());
        var clinicalCompletion = encounter.ValidateCompletion(ticket);
        if (clinicalCompletion.IsFailure) validationErrors.AddRange(clinicalCompletion.Errors);
        if (validationErrors.Count > 0) return Result<TicketDetailsResponse>.Fail(validationErrors);
        if (prescription is not null)
        {
            // Validation and all three lifecycle transitions commit in the existing transaction.
            var finalized = prescription.FinalizeInitial(encounter, actor.Value.ApplicationUserId, nowUtc);
            if (finalized.IsFailure) return Result<TicketDetailsResponse>.Fail(finalized.Errors);
        }
        var encounterCompleted = encounter.Complete(ticket, actor.Value.ApplicationUserId, nowUtc);
        if (encounterCompleted.IsFailure) return Result<TicketDetailsResponse>.Fail(encounterCompleted.Errors);
        if (ticket.FollowUpEligibilityId is { } eligibilityId)
        {
            var eligibility = await followUp.FindAsync(eligibilityId, cancellationToken);
            if (eligibility is null) return Result<TicketDetailsResponse>.Fail(FollowUpErrors.NotFound);
            var consumed = eligibility.Consume(encounter, await followUp.TodayAsync(request.PracticeId, cancellationToken),
                actor.Value.ApplicationUserId, nowUtc);
            if (consumed.IsFailure) return Result<TicketDetailsResponse>.Fail(consumed.Errors);
        }

        var completed = ticket.Complete(actor.Value.ApplicationUserId, nowUtc);
        if (completed.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(completed.Errors);
        }

        idempotency.Value.Record!.Complete(ticket.Id, nowUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await DetailsAsync(ticket.Id, cancellationToken);
    }

    private async Task<Result<TicketDetailsResponse>> DetailsAsync(
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        var details = await queueReader.GetDetailsAsync(ticketId, cancellationToken);
        return details is null
            ? Result<TicketDetailsResponse>.Fail(TicketErrors.NotFound)
            : Result<TicketDetailsResponse>.Ok(details);
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}

using System.Text.Json;
using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Persistence;
using Wasla.Domain.Clinical;
using Wasla.Domain.Security;
using Wasla.Application.Features.Medications;

namespace Wasla.Application.Features.Clinical;

internal sealed class UpdateClinicalNotesValidator : AbstractValidator<UpdateClinicalNotesCommand>
{
    public UpdateClinicalNotesValidator()
    {
        RuleFor(c => c.PracticeId).NotEmpty(); RuleFor(c => c.EncounterId).NotEmpty();
        RuleFor(c => c.ClinicalNotes).MaximumLength(8000);
        RuleFor(c => c.RowVersion).Must(TicketRowVersion.IsValid);
    }
}
internal sealed class ManageDiagnosisValidator : AbstractValidator<ManageDiagnosisCommand>
{
    public ManageDiagnosisValidator()
    {
        RuleFor(c => c.PracticeId).NotEmpty(); RuleFor(c => c.EncounterId).NotEmpty();
        RuleFor(c => c.Mutation).IsInEnum(); RuleFor(c => c.EncounterRowVersion).Must(TicketRowVersion.IsValid);
        When(c => c.Mutation != DiagnosisMutation.Remove, () =>
        {
            RuleFor(c => c.Type).NotNull().Must(t => t.HasValue && Enum.IsDefined(t.Value));
            RuleFor(c => c.DisplayText).NotEmpty().MaximumLength(500); RuleFor(c => c.Notes).MaximumLength(2000);
        });
        When(c => c.Mutation != DiagnosisMutation.Add, () => RuleFor(c => c.DiagnosisId).NotEmpty());
    }
}
internal sealed class CreateEncounterAmendmentValidator : AbstractValidator<CreateEncounterAmendmentCommand>
{
    public CreateEncounterAmendmentValidator()
    {
        RuleFor(c => c.PracticeId).NotEmpty(); RuleFor(c => c.EncounterId).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(1000);
        RuleFor(c => c.EncounterRowVersion).Must(TicketRowVersion.IsValid);
        RuleFor(c => c.Changes).NotEmpty().Must(c => c is { Count: <= 100 } && c.All(item => item is not null));
    }
}
internal sealed class CreateFollowUpEligibilityValidator : AbstractValidator<CreateFollowUpEligibilityCommand>
{
    public CreateFollowUpEligibilityValidator()
    {
        RuleFor(c => c.PracticeId).NotEmpty(); RuleFor(c => c.EncounterId).NotEmpty(); RuleFor(c => c.ValidUntil).NotEmpty();
    }
}

internal sealed class ClinicalMutationService(ClinicalAccessService access, IUnitOfWork<WaslaWritePersistence> unitOfWork,
    IClinicalReadService reader, IDateTimeProvider clock)
{
    public async Task<Result<EncounterDetailsResponse>> MutateAsync(Guid practiceId, Guid encounterId, string rowVersion,
        string permission, Func<MedicalEncounter, ClinicalActor, Result> mutate, CancellationToken ct)
    {
        var actor = await access.DoctorAsync(practiceId, permission, ct);
        if (actor.IsFailure) return Result<EncounterDetailsResponse>.Fail(actor.Errors);
        // Mutation responses include notes and diagnoses, so reading both is also required.
        if (!access.HasPermission(PermissionNames.MedicalEncountersViewOwn) || !access.HasPermission(PermissionNames.DiagnosesViewOwn))
            return Result<EncounterDetailsResponse>.Fail(ClinicalErrors.AccessDenied);
        var encounter = await unitOfWork.WriteRepository<MedicalEncounter>().FirstOrDefaultAsync(new EncounterForUpdateSpecification(encounterId), ct);
        if (encounter is null || encounter.DoctorId != actor.Value.DoctorId || encounter.DoctorPracticeId != practiceId)
            return Result<EncounterDetailsResponse>.Fail(ClinicalErrors.NotFound);
        if (!TicketRowVersion.Matches(encounter.RowVersion, rowVersion)) return Result<EncounterDetailsResponse>.Fail(ClinicalErrors.ConcurrencyConflict);
        var changed = mutate(encounter, actor.Value);
        if (changed.IsFailure) return Result<EncounterDetailsResponse>.Fail(changed.Errors);
        await unitOfWork.SaveChangesAsync(ct);
        return await DetailsAsync(actor.Value.DoctorId, practiceId, encounterId, ct);
    }

    public async Task<Result<EncounterDetailsResponse>> DetailsAsync(Guid doctorId, Guid practiceId, Guid encounterId, CancellationToken ct)
    {
        var details = await reader.DoctorDetailsAsync(doctorId, practiceId, encounterId, false, ct);
        return details is null ? Result<EncounterDetailsResponse>.Fail(ClinicalErrors.NotFound)
            : Result<EncounterDetailsResponse>.Ok(ClinicalCapabilities.Apply(details, access));
    }
    public DateTime Now => clock.UtcNow;
}

internal static class ClinicalCapabilities
{
    public static EncounterDetailsResponse Apply(EncounterDetailsResponse d, ClinicalAccessService access)
    {
        var permissions = PermissionNames.All.Where(access.HasPermission).ToArray();
        var prescription = access.HasPermission(PermissionNames.PrescriptionsViewOwn) && d.Prescription is { } state
            ? PrescriptionCapabilities.For(state, new MedicationActor(access.ActorId, d.Doctor.Id, permissions), d.Status == EncounterStatus.InProgress) : null;
        return d with { Prescription = prescription, Capabilities = For(d, access) };
    }
    public static EncounterCapabilitiesResponse For(EncounterDetailsResponse d, ClinicalAccessService access)
    {
        var draft = d.Status == EncounterStatus.InProgress;
        return new(draft && access.HasPermission(PermissionNames.MedicalEncountersUpdateOwn),
            draft && access.HasPermission(PermissionNames.DiagnosesManageOwn),
            draft && d.CompletionBlockers is not { Count: > 0 } && !string.IsNullOrWhiteSpace(d.ClinicalNotes) && access.HasPermission(PermissionNames.MedicalEncountersCompleteOwn) &&
                access.HasPermission(PermissionNames.DoctorPracticeTicketsCompleteOwn),
            !draft && access.HasPermission(PermissionNames.MedicalEncountersAmendOwn),
            !draft && d.FollowUpEligibility is null && access.HasPermission(PermissionNames.FollowUpEligibilityCreateOwn),
            draft && access.HasPermission(PermissionNames.PrescriptionsManageOwnDraft),
            draft && access.HasPermission(PermissionNames.PrescriptionsManageOwnDraft) && access.HasPermission(PermissionNames.DrugCatalogRequestsCreateOwn));
    }
}

internal sealed class UpdateClinicalNotesHandler(ClinicalMutationService service) : ICommandHandler<UpdateClinicalNotesCommand, EncounterDetailsResponse>
{
    public Task<Result<EncounterDetailsResponse>> Handle(UpdateClinicalNotesCommand r, CancellationToken ct)
        => service.MutateAsync(r.PracticeId, r.EncounterId, r.RowVersion, PermissionNames.MedicalEncountersUpdateOwn,
            (e, a) => e.UpdateClinicalNotes(r.ClinicalNotes, a.UserId, service.Now), ct);
}
internal sealed class ManageDiagnosisHandler(ClinicalMutationService service) : ICommandHandler<ManageDiagnosisCommand, EncounterDetailsResponse>
{
    public Task<Result<EncounterDetailsResponse>> Handle(ManageDiagnosisCommand r, CancellationToken ct)
        => service.MutateAsync(r.PracticeId, r.EncounterId, r.EncounterRowVersion, PermissionNames.DiagnosesManageOwn,
            (e, a) => r.Mutation switch
            {
                DiagnosisMutation.Add => e.AddDiagnosis(Guid.NewGuid(), r.Type!.Value, r.DisplayText!, r.Notes, a.UserId, service.Now),
                DiagnosisMutation.Update => e.UpdateDiagnosis(r.DiagnosisId!.Value, r.Type!.Value, r.DisplayText!, r.Notes, a.UserId, service.Now),
                DiagnosisMutation.Remove => e.RemoveDiagnosis(r.DiagnosisId!.Value, a.UserId, service.Now),
                _ => Result.Fail(ClinicalErrors.DiagnosisInvalid)
            }, ct);
}
internal sealed class CreateEncounterAmendmentHandler(ClinicalMutationService service) : ICommandHandler<CreateEncounterAmendmentCommand, EncounterDetailsResponse>
{
    public Task<Result<EncounterDetailsResponse>> Handle(CreateEncounterAmendmentCommand r, CancellationToken ct)
        => service.MutateAsync(r.PracticeId, r.EncounterId, r.EncounterRowVersion, PermissionNames.MedicalEncountersAmendOwn,
            (e, a) => e.Amend(r.Reason, r.Changes, a.DoctorId, a.UserId, service.Now, value => JsonSerializer.Serialize(value)), ct);
}
internal sealed class CreateFollowUpEligibilityHandler(ClinicalAccessService access, FollowUpWorkflow workflow,
    IUnitOfWork<WaslaWritePersistence> unitOfWork, IClinicalReadService reader, IDateTimeProvider clock)
    : ICommandHandler<CreateFollowUpEligibilityCommand, FollowUpEligibilityResponse>
{
    public async Task<Result<FollowUpEligibilityResponse>> Handle(CreateFollowUpEligibilityCommand r, CancellationToken ct)
    {
        var actor = await access.DoctorAsync(r.PracticeId, PermissionNames.FollowUpEligibilityCreateOwn, ct);
        if (actor.IsFailure) return Result<FollowUpEligibilityResponse>.Fail(actor.Errors);
        var encounter = await unitOfWork.WriteRepository<MedicalEncounter>().GetByIdAsync(r.EncounterId, ct);
        if (encounter is null || encounter.DoctorId != actor.Value.DoctorId || encounter.DoctorPracticeId != r.PracticeId)
            return Result<FollowUpEligibilityResponse>.Fail(ClinicalErrors.NotFound);
        var repository = unitOfWork.WriteRepository<FollowUpEligibility>();
        if (await repository.GetByPropertyAsync(e => e.SourceMedicalEncounterId == r.EncounterId, ct) is not null)
            return Result<FollowUpEligibilityResponse>.Fail(FollowUpErrors.AlreadyExists);
        var created = FollowUpEligibility.Create(encounter, r.ValidUntil, await workflow.TodayAsync(r.PracticeId, ct), actor.Value.UserId, clock.UtcNow);
        if (created.IsFailure) return Result<FollowUpEligibilityResponse>.Fail(created.Errors);
        await repository.AddAsync(created.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);
        var page = await reader.EligibilitiesAsync([encounter.PatientId], r.PracticeId, null, created.Value.Id, 1, 1, ct);
        return Result<FollowUpEligibilityResponse>.Ok(page.Items.Single());
    }
}

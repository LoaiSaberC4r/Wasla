using Wasla.Application.Features.Archives;
using BuildingBlock.Application.Abstraction;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;
using Wasla.Application.Features.DoctorPatients;
using BuildingBlock.Application.Repositories;
using Wasla.Domain.Practices;
using BuildingBlock.Application.Abstraction.Security;
using Wasla.Application.Persistence;

namespace Wasla.Application.Features.Tickets.GetTicketDetails;

public sealed record GetTicketDetailsQuery(Guid PracticeId, Guid TicketId)
    : IQuery<TicketDetailsResponse>;

internal sealed class GetTicketDetailsQueryValidator : AbstractValidator<GetTicketDetailsQuery>
{
    public GetTicketDetailsQueryValidator()
    {
        RuleFor(query => query.PracticeId).NotEmpty();
        RuleFor(query => query.TicketId).NotEmpty();
    }
}

internal sealed class GetTicketDetailsQueryHandler(
    TicketAccessService access,
    ITicketQueueReader queueReader,
    IArchiveLocationResolver archive, DoctorOwnHistoricalReadPolicy historicalReads, IReadRepository<DoctorPractice, WaslaReadPersistence> practices,
    IDoctorPatientReadService patientReads, ICurrentUser currentUser, DoctorPatientRelationshipPolicy relationships)
    : IQueryHandler<GetTicketDetailsQuery, TicketDetailsResponse>
{
    public async Task<Result<TicketDetailsResponse>> Handle(
        GetTicketDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var location = await archive.TicketAsync(request.TicketId, cancellationToken);
        var archived = location?.IsArchived == true;
        DoctorOwnReadContext? historical = null;
        var practice = await practices.GetByIdAsync(request.PracticeId, cancellationToken);
        if (practice is { IsActive: false } && currentUser.Roles.Contains(SystemRoleNames.Doctor, StringComparer.OrdinalIgnoreCase))
        {
            if (location is null || location.DoctorPracticeId != request.PracticeId) return Result<TicketDetailsResponse>.Fail(TicketErrors.NotFound);
            var read = await historicalReads.AuthorizeOwnReadAsync(request.PracticeId, PermissionNames.DoctorPracticeTicketsViewOwn, location.PatientId, cancellationToken);
            if (read.IsFailure) return Result<TicketDetailsResponse>.Fail(read.Errors);
            if (archived && !read.Value.Actor.Has(Phase18PartTwoPermissionNames.TicketsPractice)) return Result<TicketDetailsResponse>.Fail(TicketErrors.AccessDenied);
            historical = read.Value;
        }
        var actor = await access.AuthorizeDoctorOrReceptionAsync(
            request.PracticeId,
            archived ? Phase18PartTwoPermissionNames.TicketsPractice : PermissionNames.DoctorPracticeTicketsViewOwn,
            archived ? Phase18PartTwoPermissionNames.TicketsAssigned : PermissionNames.PracticeTicketsView,
            cancellationToken);
        if (historical is not null) actor = Result<TicketActor>.Ok(new(historical.Actor.UserId, true, false));
        if (actor.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(actor.Errors);
        }

        var details = await queueReader.GetDetailsAsync(request.TicketId, cancellationToken);
        if (details is null || details.DoctorPracticeId != request.PracticeId)
        {
            return Result<TicketDetailsResponse>.Fail(TicketErrors.NotFound);
        }
        if (historical is not null)
        {
            var latest = await historicalReads.AuthorizeOwnReadAsync(request.PracticeId, PermissionNames.DoctorPracticeTicketsViewOwn, details.PatientId, cancellationToken);
            if (latest.IsFailure) return Result<TicketDetailsResponse>.Fail(latest.Errors);
            var a = latest.Value.Actor;
            if (details.DoctorId != a.DoctorId) return Result<TicketDetailsResponse>.Fail(TicketErrors.NotFound);
            details = details with { CanRefund = false, RefundableAmount = 0, PaymentId = null, PaymentTransactionNumber = null,
                RefundId = null, RefundTransactionNumber = null, PatientsAheadNow = 0, PatientsAheadNowIsCurrent = false,
                OperationalCapabilities = new(false, false, false, false), MedicalEncounterRowVersion = null,
                MedicalEncounterId = a.Has(PermissionNames.MedicalEncountersViewOwn) && a.Has(PermissionNames.DiagnosesViewOwn) ? details.MedicalEncounterId : null,
                PrescriptionId = a.Has(PermissionNames.PrescriptionsViewOwn) ? details.PrescriptionId : null };
            if ((details.IsArchived || (await archive.TicketAsync(request.TicketId, cancellationToken))?.IsArchived == true) &&
                !a.Has(Phase18PartTwoPermissionNames.TicketsPractice)) return Result<TicketDetailsResponse>.Fail(TicketErrors.AccessDenied);
            await patientReads.AuditAsync(a, "HistoricalOwnTicketRead", request.TicketId, cancellationToken);
            return Result<TicketDetailsResponse>.Ok(details);
        }

        // Location can advance once while a detail request is being read. Recheck
        // the final location so live-only authorization cannot release an archive.
        location = await archive.TicketAsync(request.TicketId, cancellationToken);
        if (!archived && (details.IsArchived || location?.IsArchived == true))
        {
            archived = true;
            actor = await access.AuthorizeDoctorOrReceptionAsync(request.PracticeId,
                Phase18PartTwoPermissionNames.TicketsPractice, Phase18PartTwoPermissionNames.TicketsAssigned, cancellationToken);
            if (actor.IsFailure) return Result<TicketDetailsResponse>.Fail(actor.Errors);
            details = details with { IsArchived = true, ArchivedAtUtc = location?.ArchivedAtUtc ?? details.ArchivedAtUtc,
                OperationalCapabilities = new(false, false, false, false), PatientsAheadNow = 0, PatientsAheadNowIsCurrent = false };
        }

        if (details.RefundableAmount > 0 &&
            (await access.AuthorizeDoctorOrReceptionAsync(
                request.PracticeId,
                PermissionNames.DoctorPracticePaymentsRefundOwn,
                PermissionNames.PracticePaymentsRefund,
                cancellationToken)).IsSuccess)
        {
            details = details with { CanRefund = true };
        }

        if (actor.Value.IsDoctor)
        {
            var current = await relationships.ActorAsync(archived ? Phase18PartTwoPermissionNames.TicketsPractice : PermissionNames.DoctorPracticeTicketsViewOwn, cancellationToken);
            if (current.IsFailure) return Result<TicketDetailsResponse>.Fail(current.Errors);
            if (details.DoctorId != current.Value.DoctorId) return Result<TicketDetailsResponse>.Fail(TicketErrors.NotFound);
            if (!current.Value.Has(PermissionNames.MedicalEncountersViewOwn) || !current.Value.Has(PermissionNames.DiagnosesViewOwn))
                details = details with { MedicalEncounterId = null, MedicalEncounterRowVersion = null };
            if (!current.Value.Has(PermissionNames.PrescriptionsViewOwn)) details = details with { PrescriptionId = null };
            if (!current.Value.Has(PermissionNames.DoctorPracticePaymentsViewOwn) && !details.CanRefund)
                details = details with { PaymentId = null, PaymentTransactionNumber = null, RefundId = null, RefundTransactionNumber = null, RefundableAmount = 0 };
            if (!archived) await patientReads.AuditAsync(current.Value, "OwnTicketRead", request.TicketId, cancellationToken);
        }

        if (archived)
        {
            await archive.AuditStaffTicketReadAsync(request.TicketId, request.PracticeId, actor.Value.ApplicationUserId, cancellationToken);
            var finance = await access.AuthorizeDoctorOrReceptionAsync(request.PracticeId, PermissionNames.DoctorPracticePaymentsViewOwn, PermissionNames.PracticePaymentsView, cancellationToken);
            if (finance.IsFailure && !details.CanRefund) details = details with { PaymentId = null, PaymentTransactionNumber = null, RefundId = null, RefundTransactionNumber = null, RefundableAmount = 0 };
            if (!actor.Value.IsDoctor) details = details with { MedicalEncounterId = null, MedicalEncounterRowVersion = null, PrescriptionId = null };
            else
            {
                var clinical = await access.AuthorizeDoctorAsync(request.PracticeId, PermissionNames.MedicalEncountersViewOwn, cancellationToken);
                var diagnosis = await access.AuthorizeDoctorAsync(request.PracticeId, PermissionNames.DiagnosesViewOwn, cancellationToken);
                var prescription = await access.AuthorizeDoctorAsync(request.PracticeId, PermissionNames.PrescriptionsViewOwn, cancellationToken);
                if (clinical.IsFailure || diagnosis.IsFailure) details = details with { MedicalEncounterId = null, MedicalEncounterRowVersion = null };
                if (prescription.IsFailure) details = details with { PrescriptionId = null };
            }
        }
        return Result<TicketDetailsResponse>.Ok(details);
    }
}

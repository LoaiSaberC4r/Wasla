using Wasla.Application.Features.Archives;
using BuildingBlock.Application.Abstraction;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

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
    IArchiveLocationResolver archive)
    : IQueryHandler<GetTicketDetailsQuery, TicketDetailsResponse>
{
    public async Task<Result<TicketDetailsResponse>> Handle(
        GetTicketDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var location = await archive.TicketAsync(request.TicketId, cancellationToken);
        var archived = location?.IsArchived == true;
        var actor = await access.AuthorizeDoctorOrReceptionAsync(
            request.PracticeId,
            archived ? Phase18PartTwoPermissionNames.TicketsPractice : PermissionNames.DoctorPracticeTicketsViewOwn,
            archived ? Phase18PartTwoPermissionNames.TicketsAssigned : PermissionNames.PracticeTicketsView,
            cancellationToken);
        if (actor.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(actor.Errors);
        }

        var details = await queueReader.GetDetailsAsync(request.TicketId, cancellationToken);
        if (details is null || details.DoctorPracticeId != request.PracticeId)
        {
            return Result<TicketDetailsResponse>.Fail(TicketErrors.NotFound);
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

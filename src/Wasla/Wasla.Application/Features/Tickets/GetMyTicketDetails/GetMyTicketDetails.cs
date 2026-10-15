using Wasla.Application.Features.Archives;
using BuildingBlock.Application.Abstraction;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Domain.Tickets;

namespace Wasla.Application.Features.Tickets.GetMyTicketDetails;

public sealed record GetMyTicketDetailsQuery(Guid TicketId) : IQuery<TicketDetailsResponse>;

internal sealed class GetMyTicketDetailsQueryValidator : AbstractValidator<GetMyTicketDetailsQuery>
{
    public GetMyTicketDetailsQueryValidator()
        => RuleFor(query => query.TicketId).NotEmpty();
}

internal sealed class GetMyTicketDetailsQueryHandler(
    TicketAccessService access,
    ITicketQueueReader queueReader,
    IArchiveLocationResolver archive, Wasla.Application.Features.Reporting.IReportingAccessGuard guard)
    : IQueryHandler<GetMyTicketDetailsQuery, TicketDetailsResponse>
{
    public async Task<Result<TicketDetailsResponse>> Handle(
        GetMyTicketDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var location = await archive.TicketAsync(request.TicketId, cancellationToken);
        var patientId = await access.ResolveOwnPatientIdAsync(cancellationToken, location?.IsArchived == true);
        if (patientId.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(patientId.Errors);
        }

        var details = await queueReader.GetDetailsAsync(request.TicketId, cancellationToken);
        location = await archive.TicketAsync(request.TicketId, cancellationToken);
        if (details != null && (details.IsArchived || location?.IsArchived == true))
        {
            patientId = await access.ResolveOwnPatientIdAsync(cancellationToken, archived: true);
            if (patientId.IsFailure) return Result<TicketDetailsResponse>.Fail(patientId.Errors);
            details = details with { IsArchived = true, ArchivedAtUtc = location?.ArchivedAtUtc ?? details.ArchivedAtUtc,
                OperationalCapabilities = new(false, false, false, false), PatientsAheadNow = 0, PatientsAheadNowIsCurrent = false };
        }
        if (details?.PatientId == patientId.Value && details.IsArchived)
        {
            var clinical = await guard.AuthorizeAsync(Wasla.Application.Features.Reporting.ReportActor.Patient, Wasla.Domain.Security.PermissionNames.MedicalEncountersViewOwnCompleted, null, null, cancellationToken);
            var prescription = await guard.AuthorizeAsync(Wasla.Application.Features.Reporting.ReportActor.Patient, Wasla.Domain.Security.PermissionNames.PrescriptionsViewOwnCompleted, null, null, cancellationToken);
            var diagnosis = await guard.AuthorizeAsync(Wasla.Application.Features.Reporting.ReportActor.Patient, Wasla.Domain.Security.PermissionNames.DiagnosesViewOwnCompleted, null, null, cancellationToken);
            if (clinical.IsFailure || diagnosis.IsFailure) details = details with { MedicalEncounterId = null, MedicalEncounterRowVersion = null };
            if (prescription.IsFailure) details = details with { PrescriptionId = null };
            details = details with { PaymentId = null, PaymentTransactionNumber = null, RefundId = null, RefundTransactionNumber = null, RefundableAmount = 0, CanRefund = false };
        }
        return details is null || details.PatientId != patientId.Value
            ? Result<TicketDetailsResponse>.Fail(TicketErrors.NotFound)
            : Result<TicketDetailsResponse>.Ok(details);
    }
}

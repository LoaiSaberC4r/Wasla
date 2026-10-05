using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Payments;
using Wasla.Domain.Practices;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;
using Wasla.Application.Features.Clinical;
using BuildingBlock.Application.Time;

namespace Wasla.Application.Features.Tickets.GetWalkInOptions;

public sealed record WalkInOptionsResponse(
    Guid PracticeId,
    string CurrencyCode,
    IReadOnlyList<WalkInSegmentOptionResponse> Segments);

public sealed record WalkInSegmentOptionResponse(
    Guid SegmentId,
    string NameAr,
    string? NameEn,
    int Priority,
    IReadOnlyList<WalkInVisitTypeOptionResponse> VisitTypes);

public sealed record WalkInVisitTypeOptionResponse(
    Guid VisitTypeId,
    string Code,
    string NameAr,
    string? NameEn,
    decimal Price);

public interface IWalkInOptionsReader
{
    Task<IReadOnlyList<WalkInSegmentOptionResponse>> ListAsync(
        Guid practiceId,
        DoctorPracticeVisitTypeCode visitType = DoctorPracticeVisitTypeCode.NewConsultation,
        CancellationToken cancellationToken = default);
}

public sealed record GetWalkInOptionsQuery(Guid PracticeId, Guid? PatientId = null, Guid? FollowUpEligibilityId = null) : IQuery<WalkInOptionsResponse>;

internal sealed class GetWalkInOptionsQueryValidator : AbstractValidator<GetWalkInOptionsQuery>
{
    public GetWalkInOptionsQueryValidator()
    {
        RuleFor(query => query.PracticeId).NotEmpty();
    }
}

internal sealed class GetWalkInOptionsQueryHandler(
    TicketAccessService access,
    FollowUpWorkflow followUp,
    IDateTimeProvider clock,
    IReadRepository<DoctorPractice, WaslaReadPersistence> practices,
    IReadRepository<Doctor, WaslaReadPersistence> doctors,
    IReadRepository<DoctorPracticeConfiguration, WaslaReadPersistence> configurations,
    IWalkInOptionsReader reader)
    : IQueryHandler<GetWalkInOptionsQuery, WalkInOptionsResponse>
{
    public async Task<Result<WalkInOptionsResponse>> Handle(
        GetWalkInOptionsQuery request,
        CancellationToken cancellationToken)
    {
        var actor = await access.AuthorizeReceptionAsync(
            request.PracticeId, PermissionNames.PracticeTicketsCreateWalkIn, cancellationToken);
        if (actor.IsFailure)
        {
            return Result<WalkInOptionsResponse>.Fail(actor.Errors);
        }

        var practice = await practices.GetByIdAsync(request.PracticeId, cancellationToken);
        var doctor = practice is null
            ? null
            : await doctors.GetByIdAsync(practice.DoctorId, cancellationToken);
        var configuration = await configurations.GetByPropertyAsync(
            item => item.DoctorPracticeId == request.PracticeId, cancellationToken);
        if (practice is null || !practice.IsActive || doctor is null ||
            doctor.ApprovalStatus != DoctorApprovalStatus.Approved || configuration is null)
        {
            return Result<WalkInOptionsResponse>.Fail(TicketErrors.PracticeUnavailable);
        }

        if (!configuration.AllowWalkIn)
        {
            return Result<WalkInOptionsResponse>.Fail(TicketErrors.WalkInNotAllowed);
        }

        var type = DoctorPracticeVisitTypeCode.NewConsultation;
        if (request.FollowUpEligibilityId.HasValue)
        {
            var permission = await access.AuthorizeReceptionAsync(request.PracticeId,
                PermissionNames.FollowUpEligibilityViewBookingEligibility, cancellationToken);
            if (permission.IsFailure) return Result<WalkInOptionsResponse>.Fail(permission.Errors);
            var today = TicketBusinessClock.CurrentBusinessDate(clock.UtcNow, configuration.TimeZoneId);
            var eligibility = await followUp.ValidateSelectionAsync(request.FollowUpEligibilityId, DoctorPracticeVisitTypeCode.FollowUp,
                request.PatientId.GetValueOrDefault(), request.PracticeId, doctor.Id, today, cancellationToken);
            if (eligibility.IsFailure) return Result<WalkInOptionsResponse>.Fail(eligibility.Errors);
            type = DoctorPracticeVisitTypeCode.FollowUp;
        }
        var segments = await reader.ListAsync(request.PracticeId, type, cancellationToken);
        return Result<WalkInOptionsResponse>.Ok(new WalkInOptionsResponse(
            request.PracticeId, FinancialPolicy.CurrencyCode, segments));
    }
}

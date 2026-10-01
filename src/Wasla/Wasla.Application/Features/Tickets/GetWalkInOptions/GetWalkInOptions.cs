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
        CancellationToken cancellationToken = default);
}

public sealed record GetWalkInOptionsQuery(Guid PracticeId) : IQuery<WalkInOptionsResponse>;

internal sealed class GetWalkInOptionsQueryValidator : AbstractValidator<GetWalkInOptionsQuery>
{
    public GetWalkInOptionsQueryValidator()
    {
        RuleFor(query => query.PracticeId).NotEmpty();
    }
}

internal sealed class GetWalkInOptionsQueryHandler(
    TicketAccessService access,
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

        var segments = await reader.ListAsync(request.PracticeId, cancellationToken);
        return Result<WalkInOptionsResponse>.Ok(new WalkInOptionsResponse(
            request.PracticeId, FinancialPolicy.CurrencyCode, segments));
    }
}

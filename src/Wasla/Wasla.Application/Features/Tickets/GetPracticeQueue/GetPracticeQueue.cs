using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Persistence;
using Wasla.Domain.Practices;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

namespace Wasla.Application.Features.Tickets.GetPracticeQueue;

public sealed record GetPracticeQueueQuery(Guid PracticeId) : IQuery<PracticeQueueResponse>;

internal sealed class GetPracticeQueueQueryValidator : AbstractValidator<GetPracticeQueueQuery>
{
    public GetPracticeQueueQueryValidator()
        => RuleFor(query => query.PracticeId).NotEmpty();
}

internal sealed class GetPracticeQueueQueryHandler(
    TicketAccessService access,
    IReadRepository<DoctorPracticeConfiguration, WaslaReadPersistence> configurations,
    ITicketQueueReader queueReader,
    IDateTimeProvider clock)
    : IQueryHandler<GetPracticeQueueQuery, PracticeQueueResponse>
{
    public async Task<Result<PracticeQueueResponse>> Handle(
        GetPracticeQueueQuery request,
        CancellationToken cancellationToken)
    {
        var actor = await access.AuthorizeDoctorOrReceptionAsync(
            request.PracticeId,
            PermissionNames.DoctorPracticeTicketsViewOwn,
            PermissionNames.PracticeTicketsView,
            cancellationToken);
        if (actor.IsFailure)
        {
            return Result<PracticeQueueResponse>.Fail(actor.Errors);
        }

        var configuration = await configurations.GetByPropertyAsync(
            item => item.DoctorPracticeId == request.PracticeId, cancellationToken);
        if (configuration is null)
        {
            return Result<PracticeQueueResponse>.Fail(TicketErrors.PracticeUnavailable);
        }

        var businessDate = TicketBusinessClock.CurrentBusinessDate(
            clock.UtcNow, configuration.TimeZoneId);
        return Result<PracticeQueueResponse>.Ok(await queueReader.GetPracticeQueueAsync(
            request.PracticeId, businessDate, cancellationToken));
    }
}

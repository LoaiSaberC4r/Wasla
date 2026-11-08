using BuildingBlock.Application.Abstraction;
using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Patients;
using Wasla.Application.Features.Doctors;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

namespace Wasla.Application.Features.Families;

public sealed record DependentProfileResponse(Guid PatientId, string NameAr, string? NameEn, DateOnly DateOfBirth, Gender Gender,
    string? PhoneNumber, bool HasProfileImage, string RowVersion, IReadOnlyList<PatientContactResponse> Contacts);
public sealed record GetDependentProfileQuery(Guid ChildPatientId) : IQuery<DependentProfileResponse>;
internal sealed class GetDependentProfileHandler(IDependentAccessPolicy access, IWaslaDataStore store)
    : IQueryHandler<GetDependentProfileQuery, DependentProfileResponse>
{
    public async Task<Result<DependentProfileResponse>> Handle(GetDependentProfileQuery request, CancellationToken ct)
    {
        var scope = await access.AuthorizeAsync(request.ChildPatientId, FamilySelfServicePermissionNames.Profile, ct);
        if (scope.IsFailure) return Result<DependentProfileResponse>.Fail(scope.Errors);
        var patient = await store.FindPatientByIdAsync(request.ChildPatientId, ct);
        if (patient is null) return Result<DependentProfileResponse>.Fail(DependentAccessErrors.Unavailable);
        var contacts = await store.ListPatientContactsAsync(patient.Id, ct);
        if ((await access.AuthorizeAsync(request.ChildPatientId, FamilySelfServicePermissionNames.Profile, ct)).IsFailure)
            return Result<DependentProfileResponse>.Fail(DependentAccessErrors.Unavailable);
        return Result<DependentProfileResponse>.Ok(new(patient.Id, patient.NameAr, patient.NameEn, patient.DateOfBirth, patient.Gender,
            patient.PhoneNumber, patient.ProfileImageMediaKey is not null, RowVersionCodec.Encode(patient.RowVersion), contacts.Select(PatientMapper.Contact).ToArray()));
    }
}
public sealed record DependentTicketResponse(Guid TicketId, int TicketNumber, TicketStatus Status, DateOnly BusinessDate,
    string PracticeNameAr, string? PracticeNameEn, string DoctorNameAr, string? DoctorNameEn, int PatientsAheadNow, DateTime LastUpdatedOnUtc);
public sealed record GetDependentActiveTicketsQuery(Guid ChildPatientId) : IQuery<IReadOnlyList<MyActiveTicketResponse>>;
public sealed record GetDependentTicketQuery(Guid ChildPatientId, Guid TicketId) : IQuery<DependentTicketResponse>;
internal sealed class GetDependentActiveTicketsHandler(IDependentAccessPolicy access, ITicketQueueReader queue)
    : IQueryHandler<GetDependentActiveTicketsQuery, IReadOnlyList<MyActiveTicketResponse>>
{
    public async Task<Result<IReadOnlyList<MyActiveTicketResponse>>> Handle(GetDependentActiveTicketsQuery request, CancellationToken ct)
    {
        var scope = await access.AuthorizeAsync(request.ChildPatientId, FamilySelfServicePermissionNames.Tickets, ct);
        if (scope.IsFailure) return Result<IReadOnlyList<MyActiveTicketResponse>>.Fail(scope.Errors);
        var tickets = await queue.ListPatientActiveAsync(request.ChildPatientId, ct);
        return (await access.AuthorizeAsync(request.ChildPatientId, FamilySelfServicePermissionNames.Tickets, ct)).IsFailure ?
            Result<IReadOnlyList<MyActiveTicketResponse>>.Fail(DependentAccessErrors.Unavailable) : Result<IReadOnlyList<MyActiveTicketResponse>>.Ok(tickets);
    }
}
internal sealed class GetDependentTicketHandler(IDependentAccessPolicy access, ITicketQueueReader queue)
    : IQueryHandler<GetDependentTicketQuery, DependentTicketResponse>
{
    public async Task<Result<DependentTicketResponse>> Handle(GetDependentTicketQuery request, CancellationToken ct)
    {
        var scope = await access.AuthorizeAsync(request.ChildPatientId, FamilySelfServicePermissionNames.Tickets, ct);
        if (scope.IsFailure) return Result<DependentTicketResponse>.Fail(scope.Errors);
        var ticket = await queue.GetDetailsAsync(request.TicketId, ct);
        if (ticket is null || ticket.PatientId != request.ChildPatientId || ticket.IsArchived ||
            (await access.AuthorizeAsync(request.ChildPatientId, FamilySelfServicePermissionNames.Tickets, ct)).IsFailure)
            return Result<DependentTicketResponse>.Fail(DependentAccessErrors.Unavailable);
        return Result<DependentTicketResponse>.Ok(new(ticket.TicketId, ticket.TicketNumber, ticket.Status, ticket.BusinessDate,
            ticket.PracticeNameAr, ticket.PracticeNameEn, ticket.DoctorNameAr, ticket.DoctorNameEn, ticket.PatientsAheadNow, ticket.LastUpdatedOnUtc));
    }
}

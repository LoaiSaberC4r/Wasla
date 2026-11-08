using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Patients;
using Wasla.Application.Media;
using Wasla.Application.Persistence;

namespace Wasla.Application.Features.Families;

public sealed record ManageDependentProfileCommand(Guid ChildPatientId, string Operation, string RowVersion, string IdempotencyKey,
    Guid? ContactId = null, PatientContactInput? Contact = null, MediaUpload? Photo = null)
    : ICommand<DependentProfileChangeResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record DependentProfileChangeResponse(Guid ChangeId, Guid? ContactId);
public sealed record GetDependentProfileImageQuery(Guid ChildPatientId) : IQuery<PrivateMedia>;
public interface IDependentProfileManagement
{
    Task<Result<DependentProfileChangeResponse>> ExecuteAsync(ManageDependentProfileCommand command, CancellationToken ct);
    Task<Result<PrivateMedia>> ImageAsync(Guid child, CancellationToken ct);
    Task<bool> CanManageAsync(Guid child, string permission, CancellationToken ct);
}
internal sealed class ManageDependentProfileHandler(IDependentProfileManagement service) : ICommandHandler<ManageDependentProfileCommand, DependentProfileChangeResponse>
{ public Task<Result<DependentProfileChangeResponse>> Handle(ManageDependentProfileCommand request, CancellationToken ct) => service.ExecuteAsync(request, ct); }
internal sealed class GetDependentProfileImageHandler(IDependentProfileManagement service) : IQueryHandler<GetDependentProfileImageQuery, PrivateMedia>
{ public Task<Result<PrivateMedia>> Handle(GetDependentProfileImageQuery request, CancellationToken ct) => service.ImageAsync(request.ChildPatientId, ct); }

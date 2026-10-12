using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Domain.Results;
using Microsoft.Extensions.Options;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Clinical;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.Clinical;

public sealed class MedicalRecordOptions
{
    public string? OpaqueEntryKey { get; set; }
    public bool CrossDoctorSharingEnabled { get; set; }
    public bool GuardianSharingEnabled { get; set; }
}
public enum MedicalRecordMode { OwnPatient = 1, TreatingDoctor = 2, Dependent = 3 }
public sealed record MedicalRecordActor(Guid UserId, Guid DoctorId, IReadOnlyList<string> Permissions);
public sealed record TreatmentContext(Guid PatientId, Guid EncounterId);
public sealed record MedicalRecordScope(Guid ActorId, Guid PatientId, MedicalRecordMode Mode, Guid? DoctorId = null,
    Guid? PracticeId = null, Guid? TicketId = null, Guid? TreatingEncounterId = null, bool CanRequestHiddenAccess = false);
public interface ICurrentTreatmentReader
{
    Task<TreatmentContext?> CurrentAsync(Guid doctorId, Guid practiceId, Guid ticketId, CancellationToken ct);
}
public interface IMedicalRecordAccessPolicy
{
    Task<Result<MedicalRecordScope>> ScopeAsync(MedicalRecordMode mode, Guid? practiceId, Guid? ticketId, Guid? dependentId, string permission, CancellationToken ct);
    Task<Result<MedicalRecordActor>> DoctorAsync(string permission, CancellationToken ct);
}
internal sealed class MedicalRecordAccessPolicy(IWaslaDataStore store, ICurrentUser current, ICurrentTreatmentReader treatments,
    IOptions<MedicalRecordOptions> options) : IMedicalRecordAccessPolicy
{
    private async Task<UserAccessSnapshot?> SnapshotAsync(string permission, CancellationToken ct)
    {
        if (!current.IsAuthenticated || current.UserId is not { } id || !current.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase)) return null;
        var s = await store.GetAccessSnapshotAsync(id, ct);
        return s is { User.IsActive: true, User.IsFirstLogin: false } && s.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase) ? s : null;
    }
    public async Task<Result<MedicalRecordActor>> DoctorAsync(string permission, CancellationToken ct)
    {
        if (!options.Value.CrossDoctorSharingEnabled || options.Value.OpaqueEntryKey is not { Length: >= 32 }) return Result<MedicalRecordActor>.Fail(MedicalRecordErrors.Denied);
        var s = await SnapshotAsync(permission, ct);
        return s is { User.UserType: UserType.Doctor, DoctorId: { } doctor, DoctorStatus: DoctorApprovalStatus.Approved }
            ? Result<MedicalRecordActor>.Ok(new(s.User.Id, doctor, s.Permissions.Intersect(current.Permissions, StringComparer.OrdinalIgnoreCase).ToArray()))
            : Result<MedicalRecordActor>.Fail(MedicalRecordErrors.Denied);
    }
    public async Task<Result<MedicalRecordScope>> ScopeAsync(MedicalRecordMode mode, Guid? practiceId, Guid? ticketId, Guid? dependentId, string permission, CancellationToken ct)
    {
        if (mode == MedicalRecordMode.Dependent)
        {
            // Exact legal policy has not been adopted. Fail closed even if an operator enables the configuration flag.
            return Result<MedicalRecordScope>.Fail(MedicalRecordErrors.Denied);
        }
        if (mode == MedicalRecordMode.TreatingDoctor)
        {
            var actor = await DoctorAsync(permission, ct);
            if (actor.IsFailure || practiceId is null || ticketId is null) return Result<MedicalRecordScope>.Fail(MedicalRecordErrors.Denied);
            var t = await treatments.CurrentAsync(actor.Value.DoctorId, practiceId.Value, ticketId.Value, ct);
            return t is null ? Result<MedicalRecordScope>.Fail(MedicalRecordErrors.Denied)
                : Result<MedicalRecordScope>.Ok(new(actor.Value.UserId, t.PatientId, mode, actor.Value.DoctorId, practiceId, ticketId, t.EncounterId,
                    actor.Value.Permissions.Contains(PermissionNames.HiddenEncounterAccessRequestsCreate, StringComparer.OrdinalIgnoreCase)));
        }
        var s = await SnapshotAsync(permission, ct);
        if (s is not { User.UserType: UserType.Patient }) return Result<MedicalRecordScope>.Fail(MedicalRecordErrors.Denied);
        var link = await store.FindPatientAccountLinkAsync(s.User.Id, ct);
        return link is null ? Result<MedicalRecordScope>.Fail(MedicalRecordErrors.Denied)
            : Result<MedicalRecordScope>.Ok(new(s.User.Id, link.PatientId, MedicalRecordMode.OwnPatient));
    }
}

using System.Text.Json.Serialization;
using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using Wasla.Application.Persistence;
using Wasla.Domain.Clinical;

namespace Wasla.Application.Features.Clinical;

public sealed record HiddenEncounterToken(Guid ActorId, Guid DoctorId, Guid TicketId, Guid EncounterId, int Revision, DateTime IssuedAtUtc);
public sealed record RequestCapabilities(bool CanApprove, bool CanReject, bool CanRevoke, bool CanOpenGrantedRecord);
public sealed record RequestOwnerDetails(ClinicalPartyResponse Patient, Guid EncounterId, ClinicalPartyResponse RequestingDoctor, Guid RequestingPracticeId);
public sealed record HiddenAccessResponse(Guid RequestId, HiddenAccessRequestStatus Status, HiddenAccessRequestStatus EffectiveStatus,
    string Reason, DateTime RequestedAtUtc, DateTime? DecidedAtUtc, DateTime? RevokedAtUtc, bool ExpiresWhenTicketEnds,
    string RowVersion, RequestCapabilities Capabilities,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? GrantedEncounterId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RequestOwnerDetails? OwnerDetails = null);
public sealed record AccessInboxSummary(int PendingCount, int ActionableCount, int ApprovedActiveCount);
public sealed record RequestHistoryResponse(Guid HistoryId, string Action, Guid ActorApplicationUserId, DateTime OccurredAtUtc);
public sealed record RequestRouting(Guid TicketId, Guid EncounterId, Guid OwningDoctorId, Guid PracticeId);
public enum HiddenAccessRead { Sent = 1, SentDetail = 2, InboxSummary = 3, Inbox = 4, OwnerDetail = 5, History = 6 }
public enum HiddenAccessDecision { Approve = 1, Reject = 2, Revoke = 3 }
public sealed record HiddenAccessQuery(HiddenAccessRead Read, Guid? PracticeId = null, Guid? TicketId = null, Guid? RequestId = null,
    HiddenAccessRequestStatus? Status = null, string? Search = null, string? Cursor = null, int PageSize = 20) : IQuery<object>;
public sealed record RequestHiddenEncounterAccessCommand(Guid PracticeId, Guid TicketId, string HiddenEncounterToken, string Reason, string IdempotencyKey)
    : ICommand<HiddenAccessResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record DecideHiddenEncounterAccessCommand(Guid RequestId, HiddenAccessDecision Decision, string RowVersion, string? IdempotencyKey = null)
    : ICommand<HiddenAccessResponse>, ITransactionalCommand<WaslaWritePersistence>;
public interface IHiddenAccessReader
{
    Task<object?> ReadAsync(HiddenAccessQuery query, MedicalRecordActor actor, CancellationToken ct);
    Task<RequestRouting?> RoutingAsync(Guid id, CancellationToken ct);
    Task<HiddenAccessResponse?> ResponseAsync(Guid id, MedicalRecordActor actor, bool owner, CancellationToken ct);
}

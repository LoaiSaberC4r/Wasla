using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;

namespace Wasla.Domain.Clinical;

public static class MedicalRecordErrors
{
    public static Error Denied => Error.NotFound("MedicalRecord.Unavailable", ErrorMessage.GetString("MedicalRecord.Unavailable"));
    public static Error InvalidInput => Error.Validation("MedicalRecord.InvalidInput", ErrorMessage.GetString("MedicalRecord.InvalidInput"));
    public static Error InvalidState => Error.Conflict("HiddenAccessRequest.InvalidState", ErrorMessage.GetString("HiddenAccessRequest.InvalidState"));
    public static Error Expired => Error.Conflict("HiddenAccessRequest.Expired", ErrorMessage.GetString("HiddenAccessRequest.Expired"));
    public static Error Conflict => Error.Conflict("MedicalRecord.ConcurrencyConflict", ErrorMessage.GetString("MedicalRecord.ConcurrencyConflict"));
    public static Error Duplicate => Error.Conflict("HiddenAccessRequest.AlreadyRequestedForTicket", ErrorMessage.GetString("HiddenAccessRequest.AlreadyRequestedForTicket"));
}

public sealed class EncounterVisibilityHistory : Entity<Guid>
{
    private EncounterVisibilityHistory() { }
    public Guid MedicalEncounterId { get; private set; }
    public EncounterVisibility? OldVisibility { get; private set; }
    public EncounterVisibility NewVisibility { get; private set; }
    public int Revision { get; private set; }
    public Guid? ActorApplicationUserId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public string Source { get; private set; } = string.Empty;
    internal static EncounterVisibilityHistory Create(Guid encounter, EncounterVisibility? old, EncounterVisibility value,
        int revision, Guid? actor, DateTime now, string source) => new()
        { Id = Guid.NewGuid(), MedicalEncounterId = encounter, OldVisibility = old, NewVisibility = value,
            Revision = revision, ActorApplicationUserId = actor, OccurredAtUtc = now, Source = source };
}

public sealed class MedicalRecordAccessAudit : Entity<Guid>
{
    private MedicalRecordAccessAudit() { }
    public Guid ActorApplicationUserId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid? TargetEncounterId { get; private set; }
    public Guid? RequestingTicketId { get; private set; }
    public string ActorCategory { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string ResourceType { get; private set; } = string.Empty;
    public Guid? ResourceId { get; private set; }
    public string AccessDecision { get; private set; } = "Allowed";
    public string AccessReasonCode { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    public static MedicalRecordAccessAudit Create(Guid actor, Guid patient, string category, string action, string resource,
        Guid? sourceId, Guid? encounter, Guid? ticket, string correlation, DateTime now) => new()
        { Id = Guid.NewGuid(), ActorApplicationUserId = actor, PatientId = patient, ActorCategory = category, Action = action,
            ResourceType = resource, ResourceId = sourceId, TargetEncounterId = encounter, RequestingTicketId = ticket,
            AccessReasonCode = category, CorrelationId = correlation, OccurredAtUtc = now };
}

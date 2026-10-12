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

using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;

namespace Wasla.Domain.Medications;

public sealed class MedicationIdempotencyRecord : AggregateRoot<Guid>
{
    private MedicationIdempotencyRecord() { }
    public Guid ActorApplicationUserId { get; private set; }
    public string Operation { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestFingerprint { get; private set; } = string.Empty;
    public string? ResponseJson { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public static MedicationIdempotencyRecord Create(Guid actor, string operation, string key, string fingerprint, DateTime now)
        => new() { Id = Guid.NewGuid(), ActorApplicationUserId = actor, Operation = operation, IdempotencyKey = key,
            RequestFingerprint = fingerprint, CreatedAtUtc = now };
    public void Complete(string responseJson) => ResponseJson = responseJson;
}

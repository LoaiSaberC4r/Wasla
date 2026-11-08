namespace Wasla.Domain.Families;

public sealed class DependentProfileChange
{
    private DependentProfileChange() { }
    public DependentProfileChange(Guid actor, Guid parent, Guid child, string operation, string key, string fingerprint, Guid? contact, DateTime now)
    { Id = Guid.NewGuid(); ActorUserId = actor; ParentPatientId = parent; ChildPatientId = child; Operation = operation; IdempotencyKey = key; Fingerprint = fingerprint; ContactId = contact; OccurredAtUtc = now; }
    public Guid Id { get; private set; }
    public Guid ActorUserId { get; private set; }
    public Guid ParentPatientId { get; private set; }
    public Guid ChildPatientId { get; private set; }
    public string Operation { get; private set; } = "";
    public string IdempotencyKey { get; private set; } = "";
    public string Fingerprint { get; private set; } = "";
    public Guid? ContactId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
}

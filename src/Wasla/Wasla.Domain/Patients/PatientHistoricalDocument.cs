namespace Wasla.Domain.Patients;

public enum HistoricalDocumentCategory { HistoricalPrescription = 1, HistoricalLabReport = 2, HistoricalRadiologyReport = 3, SurgeryOrDischargeReport = 4, OtherMedicalDocument = 5 }
public enum HistoricalDocumentState { PendingValidation = 1, AvailableUnverified = 2, Quarantined = 3, RejectedUnsafe = 4, Withdrawn = 5 }

public sealed class PatientHistoricalDocument
{
    private PatientHistoricalDocument() { }
    public PatientHistoricalDocument(Guid subject, Guid user, Guid uploader, DateTime now)
    { Id = Guid.NewGuid(); SubjectPatientId = subject; UploadedByApplicationUserId = user; UploaderPatientId = uploader; UploadedAtUtc = now; ModifiedAtUtc = now; }
    public Guid Id { get; private set; }
    public Guid SubjectPatientId { get; private set; }
    public Guid UploadedByApplicationUserId { get; private set; }
    public Guid UploaderPatientId { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }
    public DateTime ModifiedAtUtc { get; private set; }
    public int CurrentVersionNumber { get; private set; }
    public HistoricalDocumentState State { get; private set; } = HistoricalDocumentState.PendingValidation;
    public bool LegalHold { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void PublishValidatedVersion(int number, DateTime now)
    {
        if (State == HistoricalDocumentState.Withdrawn || number != CurrentVersionNumber + 1) throw new InvalidOperationException("Invalid historical document transition.");
        CurrentVersionNumber = number; State = HistoricalDocumentState.AvailableUnverified; ModifiedAtUtc = now;
    }
    public void Withdraw(DateTime now)
    {
        if (State != HistoricalDocumentState.AvailableUnverified) throw new InvalidOperationException("Invalid historical document transition.");
        State = HistoricalDocumentState.Withdrawn; ModifiedAtUtc = now;
    }
    public void RejectValidation(bool malwareDetected, DateTime now)
    {
        if (State != HistoricalDocumentState.PendingValidation || CurrentVersionNumber != 0) throw new InvalidOperationException("Only a staged source may be rejected.");
        State = malwareDetected ? HistoricalDocumentState.RejectedUnsafe : HistoricalDocumentState.Quarantined; ModifiedAtUtc = now;
    }
}
public sealed class PatientHistoricalDocumentVersion
{
    private PatientHistoricalDocumentVersion() { }
    public PatientHistoricalDocumentVersion(Guid document, int number, HistoricalDocumentCategory category, DateOnly? date, bool unknown,
        string? provider, string? title, string? note, Guid actor, DateTime now, string? reason, DateTime? originalUploadedAtUtc = null)
    { Id = Guid.NewGuid(); DocumentId = document; VersionNumber = number; Category = category; DocumentDate = date; DateUnknown = unknown; ProviderName = provider; Title = title; PatientNote = note; CreatedByUserId = actor; CreatedAtUtc = now; Reason = reason; TimelineAtUtc = date?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) ?? originalUploadedAtUtc ?? now; }
    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public int VersionNumber { get; private set; }
    public HistoricalDocumentCategory Category { get; private set; }
    public DateOnly? DocumentDate { get; private set; }
    public bool DateUnknown { get; private set; }
    public string? ProviderName { get; private set; }
    public string? Title { get; private set; }
    public string? PatientNote { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public string? Reason { get; private set; }
    public DateTime TimelineAtUtc { get; private set; }
}
public sealed class PatientHistoricalDocumentAttachment
{
    private PatientHistoricalDocumentAttachment() { }
    public PatientHistoricalDocumentAttachment(Guid version, string key, string name, string mime, long length, string hash, DateTime scanned)
    { Id = Guid.NewGuid(); VersionId = version; MediaKey = key; FileName = name; ContentType = mime; Length = length; Sha256 = hash; ScannedAtUtc = scanned; }
    public Guid Id { get; private set; }
    public Guid VersionId { get; private set; }
    public string MediaKey { get; private set; } = "";
    public string FileName { get; private set; } = "";
    public string ContentType { get; private set; } = "";
    public long Length { get; private set; }
    public string Sha256 { get; private set; } = "";
    public DateTime ScannedAtUtc { get; private set; }
}
public sealed class PatientHistoricalDocumentHistory
{
    private PatientHistoricalDocumentHistory() { }
    public PatientHistoricalDocumentHistory(Guid doc, int version, Guid actor, string action, DateTime now)
    { Id = Guid.NewGuid(); DocumentId = doc; VersionNumber = version; ActorUserId = actor; Action = action; OccurredAtUtc = now; }
    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public int VersionNumber { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string Action { get; private set; } = "";
    public DateTime OccurredAtUtc { get; private set; }
}
public sealed class HistoricalDocumentOperation
{
    private HistoricalDocumentOperation() { }
    public HistoricalDocumentOperation(Guid actor, Guid subject, string operation, string key, string fingerprint, Guid document, int version, DateTime now,
        string validationState = "AvailableUnverified", string? failureCode = null, string? metadataJson = null)
    { Id = Guid.NewGuid(); ActorUserId = actor; SubjectPatientId = subject; Operation = operation; IdempotencyKey = key; Fingerprint = fingerprint; DocumentId = document; VersionNumber = version; CreatedAtUtc = now; ValidationState = validationState; FailureCode = failureCode; MetadataJson = metadataJson; }
    public Guid Id { get; private set; }
    public Guid ActorUserId { get; private set; }
    public Guid SubjectPatientId { get; private set; }
    public string Operation { get; private set; } = "";
    public string IdempotencyKey { get; private set; } = "";
    public string Fingerprint { get; private set; } = "";
    public Guid DocumentId { get; private set; }
    public int VersionNumber { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public string ValidationState { get; private set; } = "AvailableUnverified";
    public string? FailureCode { get; private set; }
    public string? MetadataJson { get; private set; }
}

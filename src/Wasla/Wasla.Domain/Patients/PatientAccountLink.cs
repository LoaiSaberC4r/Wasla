using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;

namespace Wasla.Domain.Patients;

public enum PatientAccountLinkSource
{
    SelfRegistration = 1,
    PatientClaim = 2
}

public sealed class PatientAccountLink : Entity<Guid>
{
    private PatientAccountLink()
    {
    }

    private PatientAccountLink(Guid id, Guid applicationUserId, Guid patientId, PatientAccountLinkSource source, DateTime verifiedOnUtc)
        : base(id)
    {
        ApplicationUserId = applicationUserId;
        PatientId = patientId;
        Source = source;
        VerifiedOnUtc = RequireUtc(verifiedOnUtc);
        CreatedOnUtc = RequireUtc(verifiedOnUtc);
    }

    public Guid ApplicationUserId { get; private set; }
    public Guid PatientId { get; private set; }
    public PatientAccountLinkSource Source { get; private set; }
    public DateTime VerifiedOnUtc { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }

    public static Result<PatientAccountLink> Create(Guid id, Guid applicationUserId, Guid patientId, PatientAccountLinkSource source, DateTime verifiedOnUtc)
    {
        if (id == Guid.Empty || applicationUserId == Guid.Empty || patientId == Guid.Empty || !Enum.IsDefined(source))
        {
            return Result<PatientAccountLink>.Fail(PatientErrors.InvalidAccountLink);
        }

        return Result<PatientAccountLink>.Ok(new PatientAccountLink(id, applicationUserId, patientId, source, verifiedOnUtc));
    }

    private static DateTime RequireUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}

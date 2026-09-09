using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace Wasla.Domain.Families;

public enum FamilyStatus
{
    Active = 1,
    Inactive = 2
}

public enum FamilyMemberRole
{
    Father = 1,
    Mother = 2,
    Child = 3,
    Guardian = 4,
    LegalGuardian = 5
}

public sealed class Family : AggregateRoot<Guid>, IAuditableEntity
{
    private readonly List<FamilyMember> _members = [];

    private Family()
    {
    }

    private Family(Guid id, Guid createdByApplicationUserId) : base(id)
    {
        Status = FamilyStatus.Active;
        CreatedByApplicationUserId = createdByApplicationUserId;
    }

    public FamilyStatus Status { get; private set; }
    public Guid CreatedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<FamilyMember> Members => _members.AsReadOnly();

    public static Result<Family> Create(Guid id, Guid createdByApplicationUserId)
        => id == Guid.Empty || createdByApplicationUserId == Guid.Empty
            ? Result<Family>.Fail(FamilyErrors.Invalid)
            : Result<Family>.Ok(new Family(id, createdByApplicationUserId));

    public Result<FamilyMember> AddMember(
        Guid memberId,
        Guid patientId,
        FamilyMemberRole role,
        DateTime verifiedOnUtc,
        Guid verifiedBySuperAdminUserId,
        Guid addedThroughRequestId)
    {
        if (Status != FamilyStatus.Active)
        {
            return Result<FamilyMember>.Fail(FamilyErrors.Inactive);
        }

        if (memberId == Guid.Empty || patientId == Guid.Empty || verifiedBySuperAdminUserId == Guid.Empty ||
            addedThroughRequestId == Guid.Empty || !Enum.IsDefined(role))
        {
            return Result<FamilyMember>.Fail(FamilyErrors.InvalidMember);
        }

        if (_members.Any(member => member.PatientId == patientId))
        {
            return Result<FamilyMember>.Fail(FamilyErrors.AlreadyMember);
        }

        if (role == FamilyMemberRole.Father && _members.Any(member => member.IsActive && member.Role == FamilyMemberRole.Father))
        {
            return Result<FamilyMember>.Fail(FamilyErrors.ActiveFatherAlreadyExists);
        }

        if (role == FamilyMemberRole.Mother && _members.Any(member => member.IsActive && member.Role == FamilyMemberRole.Mother))
        {
            return Result<FamilyMember>.Fail(FamilyErrors.ActiveMotherAlreadyExists);
        }

        var memberResult = FamilyMember.Create(
            memberId, Id, patientId, role, verifiedOnUtc, verifiedBySuperAdminUserId, addedThroughRequestId);
        if (memberResult.IsFailure)
        {
            return memberResult;
        }

        _members.Add(memberResult.Value);
        ModifiedOnUtc = RequireUtc(verifiedOnUtc);
        return memberResult;
    }

    private static DateTime RequireUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}

public sealed class FamilyMember : Entity<Guid>
{
    private FamilyMember()
    {
    }

    private FamilyMember(Guid id, Guid familyId, Guid patientId, FamilyMemberRole role, DateTime verifiedOnUtc, Guid verifiedBySuperAdminUserId, Guid addedThroughRequestId)
        : base(id)
    {
        FamilyId = familyId;
        PatientId = patientId;
        Role = role;
        IsActive = true;
        VerifiedOnUtc = RequireUtc(verifiedOnUtc);
        VerifiedBySuperAdminUserId = verifiedBySuperAdminUserId;
        AddedThroughRequestId = addedThroughRequestId;
        JoinedOnUtc = RequireUtc(verifiedOnUtc);
    }

    public Guid FamilyId { get; private set; }
    public Guid PatientId { get; private set; }
    public FamilyMemberRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime VerifiedOnUtc { get; private set; }
    public Guid VerifiedBySuperAdminUserId { get; private set; }
    public Guid AddedThroughRequestId { get; private set; }
    public DateTime JoinedOnUtc { get; private set; }

    internal static Result<FamilyMember> Create(Guid id, Guid familyId, Guid patientId, FamilyMemberRole role, DateTime verifiedOnUtc, Guid verifiedBySuperAdminUserId, Guid addedThroughRequestId)
        => id == Guid.Empty || familyId == Guid.Empty || patientId == Guid.Empty || verifiedBySuperAdminUserId == Guid.Empty ||
           addedThroughRequestId == Guid.Empty || !Enum.IsDefined(role)
            ? Result<FamilyMember>.Fail(FamilyErrors.InvalidMember)
            : Result<FamilyMember>.Ok(new FamilyMember(id, familyId, patientId, role, verifiedOnUtc, verifiedBySuperAdminUserId, addedThroughRequestId));

    public bool CanManageFamily => IsActive && Role is FamilyMemberRole.Father or FamilyMemberRole.Mother or FamilyMemberRole.Guardian or FamilyMemberRole.LegalGuardian;

    private static DateTime RequireUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}

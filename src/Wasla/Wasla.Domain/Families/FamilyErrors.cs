using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;

namespace Wasla.Domain.Families;

public static class FamilyErrors
{
    public static Error Invalid => Error.Validation("Family.Invalid", ErrorMessage.FamilyInvalid);
    public static Error Inactive => Error.Conflict("Family.Inactive", ErrorMessage.FamilyInactive);
    public static Error NotFound => Error.NotFound("Family.NotFound", ErrorMessage.FamilyNotFound);
    public static Error InvalidMember => Error.Validation("Family.MemberInvalid", ErrorMessage.FamilyMemberInvalid);
    public static Error MemberNotFound => Error.NotFound("Family.MemberNotFound", ErrorMessage.FamilyMemberNotFound);
    public static Error AlreadyMember => Error.Conflict("Family.AlreadyMember", ErrorMessage.FamilyAlreadyMember);
    public static Error PatientAlreadyInAnotherFamily => Error.Conflict("Family.PatientAlreadyInAnotherFamily", ErrorMessage.FamilyPatientAlreadyInAnotherFamily);
    public static Error ActiveFatherAlreadyExists => Error.Conflict("Family.ActiveFatherAlreadyExists", ErrorMessage.FamilyActiveFatherAlreadyExists);
    public static Error ActiveMotherAlreadyExists => Error.Conflict("Family.ActiveMotherAlreadyExists", ErrorMessage.FamilyActiveMotherAlreadyExists);
    public static Error ManagementForbidden => Error.Security("Family.ManagementForbidden", ErrorMessage.FamilyManagementForbidden);
    public static Error RequestNotFound => Error.NotFound("FamilyRelationshipRequest.NotFound", ErrorMessage.FamilyRelationshipRequestNotFound);
    public static Error RequestInvalid => Error.Validation("FamilyRelationshipRequest.Invalid", ErrorMessage.FamilyRelationshipRequestInvalid);
    public static Error RequestInvalidState => Error.Conflict("FamilyRelationshipRequest.InvalidState", ErrorMessage.FamilyRelationshipRequestInvalidState);
    public static Error RequestAlreadyFinalized => Error.Conflict("FamilyRelationshipRequest.AlreadyFinalized", ErrorMessage.FamilyRelationshipRequestAlreadyFinalized);
    public static Error ModificationRequired => Error.Validation("FamilyRelationshipRequest.ModificationRequired", ErrorMessage.FamilyRelationshipRequestModificationRequired);
    public static Error RejectionReasonRequired => Error.Validation("FamilyRelationshipRequest.RejectionReasonRequired", ErrorMessage.FamilyRelationshipRequestRejectionReasonRequired);
    public static Error EvidenceRequired => Error.Validation("FamilyRelationshipRequest.EvidenceRequired", ErrorMessage.FamilyRelationshipRequestEvidenceRequired);
    public static Error TargetInvalid => Error.Conflict("FamilyRelationshipRequest.TargetInvalid", ErrorMessage.FamilyRelationshipRequestTargetInvalid);
    public static Error RequesterInvalid => Error.Security("FamilyRelationshipRequest.RequesterInvalid", ErrorMessage.FamilyRelationshipRequestRequesterInvalid);
    public static Error OpenRequestAlreadyExists => Error.Conflict("FamilyRelationshipRequest.OpenRequestAlreadyExists", ErrorMessage.FamilyRelationshipRequestOpenAlreadyExists);
    public static Error AccessForbidden => Error.Security("FamilyRelationshipRequest.AccessForbidden", ErrorMessage.AccessForbidden);
    public static Error ConcurrencyConflict => Error.Conflict("FamilyRelationshipRequest.ConcurrencyConflict", ErrorMessage.InvalidRowVersion);
    public static Error DocumentNotFound => Error.NotFound("FamilyRelationshipRequest.DocumentNotFound", ErrorMessage.FamilyRelationshipDocumentNotFound);
}

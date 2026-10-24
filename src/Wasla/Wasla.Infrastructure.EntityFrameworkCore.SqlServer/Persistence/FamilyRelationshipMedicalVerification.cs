using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Families;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Media;
using Wasla.Domain.Families;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class FamilyRelationshipMedicalVerification(WaslaDbContext db, IOptions<FamilyMedicalPolicyOptions> options,
    ITicketQueueLock locks, IPrivateMediaReader media) : IFamilyRelationshipMedicalVerification
{
    public bool AcceptsEvidence(IReadOnlyList<FamilyEvidenceUpload> evidence) => options.Value.IsConfigured &&
        evidence.Count > 0 && evidence.Count <= options.Value.MaximumFiles && evidence.All(e =>
            options.Value.AllowedDocumentTypes.Contains(e.DocumentType) && e.File.Length > 0 && e.File.Length <= options.Value.MaximumFileBytes);

    public async Task<Result> RecordAsync(FamilyRelationshipRequest request, Family family, CancellationToken ct)
    {
        var verification = request.RequestType is FamilyRelationshipRequestType.VerifyParentChild or FamilyRelationshipRequestType.VerifyGuardianChild;
        var adult = family.Members.SingleOrDefault(m => m.IsActive && m.PatientId == request.RequesterPatientId && m.Role == request.RequesterClaimedRole);
        var child = family.Members.SingleOrDefault(m => m.IsActive && m.PatientId == request.TargetPatientId && m.Role == FamilyMemberRole.Child);
        if (family.Status != FamilyStatus.Active || adult is null || child is null || !adult.CanManageFamily)
            return verification ? Result.Fail(FamilyMedicalErrors.Unavailable) : Result.Ok();
        // Ordinary membership approval remains independent of medical policy. It grants no medical proof when policy/evidence is unavailable.
        if (!options.Value.IsConfigured) return verification ? Result.Fail(FamilyMedicalErrors.PolicyUnavailable) : Result.Ok();
        if (!await db.SuperAdmins.AsNoTracking().AnyAsync(s => s.ApplicationUserId == request.ReviewedBySuperAdminApplicationUserId &&
            db.ApplicationUsers.Any(u => u.Id == s.ApplicationUserId && u.UserType == Wasla.Domain.Common.UserType.SuperAdmin), ct))
            return verification ? Result.Fail(FamilyMedicalErrors.Unavailable) : Result.Ok();
        var documents = await db.FamilyRelationshipDocuments.AsNoTracking().Where(d => d.FamilyRelationshipRequestId == request.Id &&
            d.RevisionNumber == request.CurrentRevisionNumber).ToArrayAsync(ct);
        if (documents.Length == 0 || documents.Length > options.Value.MaximumFiles || documents.Any(d =>
            !options.Value.AllowedDocumentTypes.Contains(d.DocumentType) || d.FileSize <= 0 || d.FileSize > options.Value.MaximumFileBytes))
            return verification ? Result.Fail(FamilyMedicalErrors.PolicyUnavailable) : Result.Ok();
        foreach (var document in documents)
        {
            PrivateMedia? opened;
            try { opened = await media.OpenAsync(document.MediaKey, ct); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { opened = null; }
            if (opened is null) return verification ? Result.Fail(FamilyMedicalErrors.PolicyUnavailable) : Result.Ok();
            await using var content = opened.Content;
            if (!content.CanSeek || content.Length != document.FileSize)
                return verification ? Result.Fail(FamilyMedicalErrors.PolicyUnavailable) : Result.Ok();
        }
        if (adult.Role is not (FamilyMemberRole.Father or FamilyMemberRole.Mother)) return Result.Ok();
        await locks.AcquireIdempotencyAsync(Guid.Empty, "Phase16:FamilyPair", $"{family.Id:N}:{adult.PatientId:N}:{child.PatientId:N}", ct);
        if (await db.Set<VerifiedParentChildRelationship>().AnyAsync(r => r.ApprovedRelationshipRequestId == request.Id &&
            r.Status == ParentChildVerificationStatus.Revoked, ct)) return Result.Ok();
        var existing = await db.Set<VerifiedParentChildRelationship>().SingleOrDefaultAsync(r => r.FamilyId == family.Id &&
            r.ParentPatientId == adult.PatientId && r.ChildPatientId == child.PatientId && r.Status == ParentChildVerificationStatus.Active, ct);
        if (existing is not null)
        {
            if (existing.ApprovedRelationshipRequestId == request.Id) return Result.Ok();
            existing.Revoke(request.ReviewedOnUtc!.Value);
            await db.SaveChangesAsync(ct); // Release filtered uniqueness inside the enclosing command transaction.
        }
        db.Add(VerifiedParentChildRelationship.Verify(adult, child, request, options.Value.PolicyVersion!));
        return Result.Ok();
    }
}

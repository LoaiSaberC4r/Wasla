using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Families;
using Wasla.Application.Media;
using Wasla.Domain.Families;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class FamilyMedicalWorkflow
{
    private static string ReadPermission(string op) => op switch
    {
        "admin-summary" or "admin-list" => PermissionNames.FamilyMedicalAccessRequestsViewAll,
        "admin-detail" or "admin-history" => PermissionNames.FamilyMedicalAccessRequestsViewAdminDetails,
        "eligibility-list" or "eligibility-detail" => PermissionNames.FamilyMedicalAccessEligibilityReview,
        "status" => PermissionNames.FamiliesViewOwn, _ => PermissionNames.FamilyMedicalAccessRequestsViewOwn
    };
    public async Task<Result<object>> ReadAsync(FamilyMedicalQuery q, CancellationToken ct)
    {
        var admin = q.Operation.StartsWith("admin-", StringComparison.Ordinal) || q.Operation.StartsWith("eligibility-", StringComparison.Ordinal);
        if (await access.ActorAsync(ReadPermission(q.Operation), admin, ct) is null) return Result<object>.Fail(FamilyMedicalErrors.Unavailable);
        if (q.Operation == "status")
        { var s = await access.StatusAsync(q.Id!.Value, ct); return s.IsFailure ? Result<object>.Fail(s.Errors) : Result<object>.Ok(s.Value); }
        if (q.Operation.StartsWith("eligibility-", StringComparison.Ordinal)) return await ReadEligibilityAsync(q, ct);
        var query = db.Set<FamilyMedicalAccessRequest>().AsNoTracking().AsQueryable();
        if (!admin)
        {
            var patient = await access.PatientAsync(ReadPermission(q.Operation), ct);
            if (patient is null) return Result<object>.Fail(FamilyMedicalErrors.Unavailable);
            var member = await access.MemberAsync(patient.Value, ct);
            if (member is null || !member.CanManageFamily) return Result<object>.Fail(FamilyMedicalErrors.Unavailable);
            query = query.Where(r => r.SubmittedByPatientId == patient && r.SubmittedByApplicationUserId == current.UserId && r.FamilyId == member.FamilyId);
        }
        if (q.Operation.EndsWith("detail", StringComparison.Ordinal) || q.Operation.EndsWith("history", StringComparison.Ordinal))
        {
            var r = await query.SingleOrDefaultAsync(r => r.Id == q.Id, ct);
            if (r is null) return Result<object>.Fail(FamilyMedicalErrors.Unavailable);
            if (q.Operation.EndsWith("history", StringComparison.Ordinal))
            {
                var events = db.Set<FamilyMedicalAccessRequestHistory>().AsNoTracking().Where(h => h.ParentId == r.Id);
                var total = await events.CountAsync(ct);
                var history = await events.OrderBy(h => h.OccurredAtUtc).ThenBy(h => h.Id).Skip((q.PageNumber - 1) * q.PageSize).Take(q.PageSize).ToArrayAsync(ct);
                object[] rows = history.Select(h => admin ? (object)new FamilyMedicalAdminHistoryResponse(h.Id, h.Action, h.OldStatus, h.NewStatus, h.Revision, h.ActorApplicationUserId, Utc(h.OccurredAtUtc), h.ReasonOrMessage)
                    : new FamilyMedicalHistoryResponse(h.Id, h.Action, h.OldStatus, h.NewStatus, h.Revision, Utc(h.OccurredAtUtc), h.ReasonOrMessage)).ToArray();
                return Result<object>.Ok(new FamilyMedicalPage<object>(rows, q.PageNumber, q.PageSize, total));
            }
            var response = await CaseAsync(r, admin, ct);
            if (!admin) return Result<object>.Ok(response);
            var memberIds = new[] { r.ChildPatientId, r.AffectedAdultPatientId, r.SubmittedByPatientId };
            var relationship = await db.FamilyMembers.AsNoTracking().Where(m => m.FamilyId == r.FamilyId && memberIds.Contains(m.PatientId))
                .Select(m => new FamilyMedicalRelationshipVerification(m.Id, m.PatientId, m.Role, m.IsActive, DateTime.SpecifyKind(m.VerifiedOnUtc, DateTimeKind.Utc), m.VerifiedBySuperAdminUserId, m.AddedThroughRequestId)).ToArrayAsync(ct);
            return Result<object>.Ok(new FamilyMedicalAdminCaseResponse(response, r.SubmittedByPatientId, r.SubmittedByApplicationUserId, relationship));
        }
        if (q.Operation == "admin-summary")
        {
            var counts = await query.GroupBy(r => new { r.RequestType, r.Status }).Select(g => new FamilyMedicalSummaryCount(g.Key.RequestType, g.Key.Status, g.Count())).ToArrayAsync(ct);
            return Result<object>.Ok(new FamilyMedicalSummaryResponse(counts));
        }
        if (q.RequestType.HasValue) query = query.Where(r => r.RequestType == q.RequestType);
        if (q.Status.HasValue) query = query.Where(r => (int)r.Status == q.Status);
        if (q.FamilyId.HasValue) query = query.Where(r => r.FamilyId == q.FamilyId);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            // Exact identifier search avoids clinical or sensitive justification full-text searching.
            if (!Guid.TryParse(q.Search, out var search)) return Result<object>.Ok(new FamilyMedicalPage<object>([], q.PageNumber, q.PageSize, 0));
            query = query.Where(r => r.Id == search || r.FamilyId == search || r.ChildPatientId == search || r.AffectedAdultPatientId == search);
        }
        var count = await query.CountAsync(ct);
        var list = await query.OrderByDescending(r => r.SubmittedAtUtc).ThenBy(r => r.Id).Skip((q.PageNumber - 1) * q.PageSize).Take(q.PageSize)
            .Select(r => new { r.Id, r.FamilyId, r.ChildPatientId, r.AffectedAdultPatientId, r.RequestType, r.Status, r.CurrentRevisionNumber, r.SubmittedAtUtc, r.RowVersion }).ToArrayAsync(ct);
        return Result<object>.Ok(new FamilyMedicalPage<FamilyMedicalQueueResponse>(list.Select(r => new FamilyMedicalQueueResponse(r.Id, r.FamilyId, r.ChildPatientId, r.AffectedAdultPatientId, r.RequestType, r.Status, r.CurrentRevisionNumber, Utc(r.SubmittedAtUtc), Convert.ToBase64String(r.RowVersion))).ToArray(), q.PageNumber, q.PageSize, count));
    }
    private async Task<FamilyMedicalCaseResponse> CaseAsync(FamilyMedicalAccessRequest r, bool admin, CancellationToken ct)
    {
        var documents = await db.Set<FamilyMedicalAccessRequestDocument>().AsNoTracking().Where(d => d.RequestId == r.Id).OrderBy(d => d.RevisionNumber).ThenBy(d => d.Id)
            .Select(d => new FamilyMedicalDocumentResponse(d.Id, d.RevisionNumber, d.DocumentType, d.FileName, d.ContentType, d.SizeBytes, true)).ToArrayAsync(ct);
        var restriction = r.AffectedRestrictionId is { } id ? await db.Set<FamilyMedicalAccessRestriction>().Where(x => x.Id == id).Select(x => (FamilyMedicalRestrictionStatus?)x.Status).SingleOrDefaultAsync(ct) : null;
        var permissions = await access.PermissionsAsync(ct);
        bool Has(string p) => permissions.Contains(p, StringComparer.OrdinalIgnoreCase);
        var caps = new FamilyMedicalRequestCapabilities(!admin && r.IsOpen && Has(PermissionNames.FamilyMedicalAccessRequestsWithdrawOwn),
            !admin && r.Status == FamilyMedicalRequestStatus.ModificationRequested && Has(PermissionNames.FamilyMedicalAccessRequestsResubmitOwn),
            admin && r.Status == FamilyMedicalRequestStatus.Pending && Has(PermissionNames.FamilyMedicalAccessRequestsDecide) && access.Configured,
            admin && r.Status == FamilyMedicalRequestStatus.Pending && Has(PermissionNames.FamilyMedicalAccessRequestsDecide),
            admin && r.Status == FamilyMedicalRequestStatus.Pending && Has(PermissionNames.FamilyMedicalAccessRequestsRequestModification));
        return new(r.Id, r.FamilyId, r.ChildPatientId, r.AffectedAdultPatientId, r.RequestType, r.Status, r.CurrentRevisionNumber, r.Reason,
            Utc(r.SubmittedAtUtc), Convert.ToBase64String(r.RowVersion), r.ReviewMessage, restriction?.ToString(), caps, documents);
    }
    private async Task<Result<object>> ReadEligibilityAsync(FamilyMedicalQuery q, CancellationToken ct)
    {
        var query = db.Set<FamilyMedicalEligibilityReview>().AsNoTracking().AsQueryable();
        if (q.Operation == "eligibility-detail")
        { var r = await query.SingleOrDefaultAsync(r => r.Id == q.Id, ct); return r is null ? Result<object>.Fail(FamilyMedicalErrors.Unavailable) : Result<object>.Ok(await EligibilityResponseAsync(r, ct)); }
        query = query.Where(r => db.FamilyMembers.Any(m => m.Id == r.AdultFamilyMemberId &&
            (m.Role == FamilyMemberRole.Guardian || m.Role == FamilyMemberRole.LegalGuardian)));
        if (q.Status.HasValue) query = query.Where(r => (int)r.Status == q.Status);
        if (q.FamilyId.HasValue) query = query.Where(r => r.FamilyId == q.FamilyId);
        if (!string.IsNullOrWhiteSpace(q.Search))
        { if (!Guid.TryParse(q.Search, out var id)) return Result<object>.Ok(new FamilyMedicalPage<object>([], q.PageNumber, q.PageSize, 0)); query = query.Where(r => r.Id == id || r.FamilyId == id || r.AdultPatientId == id || r.ChildPatientId == id); }
        var count = await query.CountAsync(ct);
        var items = await query.OrderBy(r => r.CreatedAtUtc).ThenBy(r => r.Id).Skip((q.PageNumber - 1) * q.PageSize).Take(q.PageSize)
            .Select(r => new { ReviewId = r.Id, r.FamilyId, r.AdultPatientId, r.ChildPatientId, r.Status, r.PolicyVersion, r.RowVersion }).ToArrayAsync(ct);
        return Result<object>.Ok(new FamilyMedicalPage<FamilyMedicalEligibilityQueueResponse>(items.Select(r => new FamilyMedicalEligibilityQueueResponse(r.ReviewId, r.FamilyId, r.AdultPatientId, r.ChildPatientId, r.Status, r.PolicyVersion, Convert.ToBase64String(r.RowVersion))).ToArray(), q.PageNumber, q.PageSize, count));
    }
    private async Task<FamilyMedicalEligibilityResponse> EligibilityResponseAsync(FamilyMedicalEligibilityReview r, CancellationToken ct)
    {
        var members = await db.FamilyMembers.AsNoTracking().Where(m => m.Id == r.AdultFamilyMemberId || m.Id == r.ChildFamilyMemberId).ToArrayAsync(ct);
        FamilyMedicalVerificationReference Reference(Guid id) { var m = members.Single(m => m.Id == id); return new(m.Id, m.AddedThroughRequestId, Utc(m.VerifiedOnUtc)); }
        var permissions = await access.PermissionsAsync(ct);
        bool Has(string p) => permissions.Contains(p, StringComparer.OrdinalIgnoreCase);
        var history = await db.Set<FamilyMedicalEligibilityReviewHistory>().AsNoTracking().Where(h => h.ParentId == r.Id).OrderByDescending(h => h.OccurredAtUtc).ThenBy(h => h.Id).Take(100)
            .Select(h => new FamilyMedicalAdminHistoryResponse(h.Id, h.Action, h.OldStatus, h.NewStatus, h.Revision, h.ActorApplicationUserId, DateTime.SpecifyKind(h.OccurredAtUtc, DateTimeKind.Utc), h.ReasonOrMessage)).ToArrayAsync(ct);
        return new(r.Id, r.FamilyId, r.AdultPatientId, r.ChildPatientId, r.Status, r.PolicyVersion, r.BasisReference, Convert.ToBase64String(r.RowVersion),
            Reference(r.AdultFamilyMemberId), Reference(r.ChildFamilyMemberId), members.Any(m => m.Id == r.AdultFamilyMemberId && m.Role is FamilyMemberRole.Guardian or FamilyMemberRole.LegalGuardian) && access.Configured && Has(PermissionNames.FamilyMedicalAccessEligibilityDecide),
            r.Status == FamilyMedicalEligibilityStatus.PendingReview && Has(PermissionNames.FamilyMedicalAccessEligibilityDecide), r.Status == FamilyMedicalEligibilityStatus.Approved && Has(PermissionNames.FamilyMedicalAccessEligibilityRevoke), r.ReviewReason, history, r.AuthorityRelationshipRequestId);
    }
    public async Task<Result<PrivateMedia>> EvidenceAsync(FamilyMedicalEvidenceQuery q, CancellationToken ct)
    {
        var permission = q.Admin ? PermissionNames.FamilyMedicalAccessRequestsViewAdminDocuments : PermissionNames.FamilyMedicalAccessRequestsViewOwnDocuments;
        async Task<bool> Allowed()
        {
            if (await access.ActorAsync(permission, q.Admin, ct) is null) return false;
            var request = await db.Set<FamilyMedicalAccessRequest>().AsNoTracking().SingleOrDefaultAsync(r => r.Id == q.RequestId, ct);
            if (request is null) return false;
            if (q.Admin) return true;
            var patient = await access.PatientAsync(permission, ct); var member = patient is { } p ? await access.MemberAsync(p, ct) : null;
            return member is { CanManageFamily: true } && request.FamilyId == member.FamilyId && request.SubmittedByPatientId == patient && request.SubmittedByApplicationUserId == current.UserId;
        }
        if (!await Allowed()) return Result<PrivateMedia>.Fail(FamilyMedicalErrors.Unavailable);
        var doc = await db.Set<FamilyMedicalAccessRequestDocument>().AsNoTracking().SingleOrDefaultAsync(d => d.Id == q.DocumentId && d.RequestId == q.RequestId, ct);
        if (doc is null) return Result<PrivateMedia>.Fail(FamilyMedicalErrors.Unavailable);
        PrivateMedia? opened;
        try { opened = await privateMedia.OpenAsync(doc.MediaKey, ct); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return Result<PrivateMedia>.Fail(FamilyMedicalErrors.Unavailable); }
        if (opened is null) return Result<PrivateMedia>.Fail(FamilyMedicalErrors.Unavailable);
        if (!await Allowed() || opened.Content.CanSeek && opened.Content.Length != doc.SizeBytes)
        { await opened.Content.DisposeAsync(); return Result<PrivateMedia>.Fail(FamilyMedicalErrors.Unavailable); }
        return Result<PrivateMedia>.Ok(opened with { FileName = Path.GetFileName(doc.FileName), ContentType = doc.ContentType });
    }
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Doctors;
using Wasla.Application.Features.Patients;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Families;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.Families;

public sealed record FamilyCandidateSearch(string NameAr, DateOnly DateOfBirth, Gender Gender, string? PhoneNumber);
public sealed record FamilyCandidateResponse(string CandidateReference, string DisplayNameMasked, int BirthYear, DateTime ExpiresAtUtc);
public sealed record FamilyCandidateSearchResponse(string SearchStatus, IReadOnlyList<FamilyCandidateResponse> Candidates, bool HasMore = false);
public sealed record SearchFamilyCandidatesCommand(FamilyCandidateSearch Criteria) : ICommand<FamilyCandidateSearchResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record FamilyCandidateSubmissionResponse(Guid? RequestId, string Status, Guid? ConflictCaseId, string NextAction);
public sealed record SubmitCandidateRelationshipCommand(string CandidateReference, FamilyMemberRole RelationshipType,
    IReadOnlyList<FamilyEvidenceUpload> Evidence, string IdempotencyKey) : ICommand<FamilyCandidateSubmissionResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record CandidateRedemption(Guid TargetPatientId, Guid? ReplayRequestId, Guid? ReplayGovernanceCaseId = null);
public interface IFamilyCandidateStore
{
    Task<Result<FamilyCandidateSearchResponse>> SearchAsync(Guid actor, Guid parent, FamilyCandidateSearch criteria, CancellationToken ct);
    Task<Result<CandidateRedemption>> ResolveAsync(Guid actor, Guid parent, string reference, string key, string fingerprint, CancellationToken ct);
    Task CompleteConflictAsync(Guid actor, string reference, string key, string fingerprint, Guid caseId, CancellationToken ct);
    Task CompleteAsync(Guid actor, string reference, string key, string fingerprint, Guid request, CancellationToken ct);
}
internal sealed class SearchFamilyCandidatesValidator : AbstractValidator<SearchFamilyCandidatesCommand>
{
    public SearchFamilyCandidatesValidator()
    {
        RuleFor(x => x.Criteria.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Criteria.DateOfBirth).NotEmpty();
        RuleFor(x => x.Criteria.Gender).Must(g => g is Gender.Male or Gender.Female);
        RuleFor(x => x.Criteria.PhoneNumber).MaximumLength(30);
    }
}
internal sealed class SubmitCandidateRelationshipValidator : AbstractValidator<SubmitCandidateRelationshipCommand>
{
    public SubmitCandidateRelationshipValidator()
    {
        RuleFor(x => x.CandidateReference).NotEmpty().MaximumLength(64);
        RuleFor(x => x.RelationshipType).Must(r => r is FamilyMemberRole.Father or FamilyMemberRole.Mother);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Evidence).Must(e => e is { Count: > 0 and <= 5 });
        RuleForEach(x => x.Evidence).ChildRules(v => v.RuleFor(e => e.DocumentType).IsInEnum());
    }
}
internal sealed class SearchFamilyCandidatesHandler(IWaslaDataStore store, ICurrentUser current, IFamilyCandidateStore candidates,
    IDateTimeProvider clock, IOptions<DependentManagementOptions> options) : ICommandHandler<SearchFamilyCandidatesCommand, FamilyCandidateSearchResponse>
{
    public async Task<Result<FamilyCandidateSearchResponse>> Handle(SearchFamilyCandidatesCommand request, CancellationToken ct)
    {
        var patient = await FamilySelfServiceActor.ResolveAsync(store, current, FamilySelfServicePermissionNames.Search, clock, options.Value, ct);
        if (patient.IsFailure) return Result<FamilyCandidateSearchResponse>.Fail(patient.Errors);
        var today = options.Value.Today(clock.UtcNow);
        if (request.Criteria.DateOfBirth > today || request.Criteria.DateOfBirth <= today.AddYears(-18))
            return Result<FamilyCandidateSearchResponse>.Fail(Error.Validation("FamilyCandidate.SearchInvalid", Wasla.Domain.Resources.FamilySelfServiceText.Get("FamilyCandidate_SearchInvalid")));
        return await candidates.SearchAsync(current.UserId!.Value, patient.Value.Id, request.Criteria, ct);
    }
}
internal sealed class SubmitCandidateRelationshipHandler(IWaslaDataStore store, ICurrentUser current, IFamilyCandidateStore candidates,
    FamilyRelationshipWorkflowService workflow, IFamilyGovernanceWorkflow governance, IDateTimeProvider clock, IOptions<DependentManagementOptions> options)
    : ICommandHandler<SubmitCandidateRelationshipCommand, FamilyCandidateSubmissionResponse>
{
    public async Task<Result<FamilyCandidateSubmissionResponse>> Handle(SubmitCandidateRelationshipCommand request, CancellationToken ct)
    {
        var parent = await FamilySelfServiceActor.ResolveAsync(store, current, FamilySelfServicePermissionNames.Submit, clock, options.Value, ct);
        if (parent.IsFailure) return Result<FamilyCandidateSubmissionResponse>.Fail(parent.Errors);
        if (request.Evidence.Count == 0 || request.Evidence.Count > options.Value.MaxEvidenceFiles || request.Evidence.Any(e => e.File.Length <= 0 || e.File.Length > options.Value.MaxEvidenceFileBytes))
            return Result<FamilyCandidateSubmissionResponse>.Fail(FamilyErrors.EvidenceRequired);
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        hash.AppendData(System.Text.Encoding.UTF8.GetBytes($"{request.RelationshipType}:{request.CandidateReference}"));
        foreach (var item in request.Evidence)
        {
            if (!item.File.Content.CanSeek) return Result<FamilyCandidateSubmissionResponse>.Fail(FamilyErrors.EvidenceRequired);
            hash.AppendData(System.Text.Encoding.UTF8.GetBytes($"{item.DocumentType}:{item.File.FileName}:{item.File.Length}"));
            var digest = await System.Security.Cryptography.SHA256.HashDataAsync(item.File.Content, ct);
            item.File.Content.Position = 0; hash.AppendData(digest);
        }
        var fingerprint = Convert.ToHexString(hash.GetHashAndReset());
        var selected = await candidates.ResolveAsync(current.UserId!.Value, parent.Value.Id, request.CandidateReference, request.IdempotencyKey, fingerprint, ct);
        if (selected.IsFailure) return Result<FamilyCandidateSubmissionResponse>.Fail(selected.Errors);
        if (selected.Value.ReplayRequestId is { } replay)
        {
            var previous = await store.FindFamilyRelationshipRequestAsync(replay, ct);
            return previous is null ? Result<FamilyCandidateSubmissionResponse>.Fail(FamilyErrors.RequestNotFound) :
                Result<FamilyCandidateSubmissionResponse>.Ok(new(previous.Request.Id, previous.Request.Status.ToString(), null, "WaitForReview"));
        }
        if (selected.Value.ReplayGovernanceCaseId is { } replayCase)
            return Result<FamilyCandidateSubmissionResponse>.Ok(new(null, "ConflictReview", replayCase, "WaitForConfidentialReview"));
        var parentMember = await store.FindActiveFamilyMemberByPatientIdAsync(parent.Value.Id, ct);
        var childMember = await store.FindActiveFamilyMemberByPatientIdAsync(selected.Value.TargetPatientId, ct);
        if (childMember is not null && parentMember is not null && parentMember.FamilyId != childMember.FamilyId)
        {
            var review = await governance.OpenConflictAsync(current.UserId.Value, parent.Value.Id, selected.Value.TargetPatientId, request.RelationshipType, request.Evidence, request.IdempotencyKey, fingerprint, ct);
            if (review.IsFailure) return Result<FamilyCandidateSubmissionResponse>.Fail(review.Errors);
            await candidates.CompleteConflictAsync(current.UserId.Value, request.CandidateReference, request.IdempotencyKey, fingerprint, review.Value.CaseId, ct);
            await store.SaveChangesAsync(ct);
            return Result<FamilyCandidateSubmissionResponse>.Ok(new(null, "ConflictReview", review.Value.CaseId, "WaitForConfidentialReview"));
        }
        var type = childMember is not null ? (parentMember is null ? FamilyRelationshipRequestType.JoinFamilyAsParent : FamilyRelationshipRequestType.VerifyOperationalParentChild) :
            parentMember is null ? FamilyRelationshipRequestType.CreateFamily : FamilyRelationshipRequestType.AddFamilyMember;
        var result = await workflow.SubmitAsync(type, childMember?.FamilyId ?? parentMember?.FamilyId, parent.Value.Id, selected.Value.TargetPatientId,
            request.RelationshipType, FamilyMemberRole.Child, request.Evidence, current.UserId.Value, ct);
        if (result.IsFailure) return Result<FamilyCandidateSubmissionResponse>.Fail(result.Errors);
        await candidates.CompleteAsync(current.UserId.Value, request.CandidateReference, request.IdempotencyKey, fingerprint, result.Value.RequestId, ct);
        await store.SaveChangesAsync(ct);
        return Result<FamilyCandidateSubmissionResponse>.Ok(new(result.Value.RequestId, result.Value.Status.ToString(), null, "WaitForReview"));
    }
}

public static class FamilySelfServiceActor
{
    public static async Task<Result<Wasla.Domain.Patients.Patient>> ResolveAsync(IWaslaDataStore store, ICurrentUser current, string permission,
        IDateTimeProvider clock, DependentManagementOptions options, CancellationToken ct)
    {
        if (!options.Enabled || !current.Permissions.Contains(permission)) return Result<Wasla.Domain.Patients.Patient>.Fail(DependentAccessErrors.Unavailable);
        var patient = await PatientIdentity.ResolveAsync(store, current, ct, permission);
        return patient.IsSuccess && patient.Value.GetAge(options.Today(clock.UtcNow)) < 18 ?
            Result<Wasla.Domain.Patients.Patient>.Fail(DependentAccessErrors.Unavailable) : patient;
    }
}

public sealed record FamilyWorkspaceChild(Guid PatientId, string NameAr, bool CanBook, bool CanViewReservations, bool CanViewQueue, bool CanViewMedicalRecord,
    bool CanViewProfile, bool CanManageContacts, bool CanUploadHistoricalDocument, bool CanViewReceipts, string? OperationalReasonCode, string? MedicalReasonCode,
    bool CanManagePhoto = false, bool CanSubmitDoctorOrderedDiagnostic = false, bool CanRequestVerification = false, bool CanCorrectIdentity = false,
    IReadOnlyDictionary<string, string?>? CapabilityReasons = null);
public sealed record FamilyWorkspaceResponse(bool HasFamily, MyFamilyResponse? Family, IReadOnlyList<FamilyWorkspaceChild> Dependents,
    IReadOnlyList<FamilyRelationshipRequestQueueResponse> PendingRequests, bool CanStartExistingChildLink, bool CanStartChildOnboarding, bool CanJoinExistingFamily);
public sealed record GetFamilyWorkspaceQuery : IQuery<FamilyWorkspaceResponse>;
internal sealed class GetFamilyWorkspaceHandler(IWaslaDataStore store, ICurrentUser current, IDependentAccessPolicy dependent,
    IFamilyMedicalAccessPolicy medical, IDateTimeProvider clock, IOptions<DependentManagementOptions> options,
    IDependentProfileManagement profiles, IOperationalAuthorityReader proofReader, IFamilyMedicalOperationPolicy medicalOperations, IOptions<Wasla.Application.Features.Clinical.HistoricalDocumentOptions> historical) : IQueryHandler<GetFamilyWorkspaceQuery, FamilyWorkspaceResponse>
{
    public async Task<Result<FamilyWorkspaceResponse>> Handle(GetFamilyWorkspaceQuery request, CancellationToken ct)
    {
        var actor = await FamilySelfServiceActor.ResolveAsync(store, current, FamilySelfServicePermissionNames.Workspace, clock, options.Value, ct);
        if (actor.IsFailure) return Result<FamilyWorkspaceResponse>.Fail(actor.Errors);
        var pending = await FamilyWorkflowMapper.ListAsync(store, null, null, null, current.UserId, actor.Value.Id, 1, 20, ct);
        var snapshot = await store.GetAccessSnapshotAsync(current.UserId!.Value, ct);
        bool Has(string p) => current.Permissions.Contains(p) && snapshot!.Permissions.Contains(p);
        var member = await store.FindActiveFamilyMemberByPatientIdAsync(actor.Value.Id, ct);
        MyFamilyResponse? family = null; var children = new List<FamilyWorkspaceChild>();
        if (member is not null)
        {
            var aggregate = await store.FindFamilyByIdAsync(member.FamilyId, ct);
            if (aggregate is not null)
            {
                var members = await store.ListActiveFamilyMembersAsync(aggregate.Id, ct);
                family = new(aggregate.Id, aggregate.Status, members.Select(FamilyWorkflowMapper.Member).ToArray());
                foreach (var child in members.Where(m => m.Member.Role == FamilyMemberRole.Child))
                {
                    var book = await dependent.AuthorizeAsync(child.Patient.Id, PermissionNames.ReservationsCreateDependents, ct);
                    var view = await dependent.AuthorizeAsync(child.Patient.Id, PermissionNames.ReservationsViewDependents, ct);
                    var queue = await dependent.AuthorizeAsync(child.Patient.Id, FamilySelfServicePermissionNames.Tickets, ct);
                    var profile = await dependent.AuthorizeAsync(child.Patient.Id, FamilySelfServicePermissionNames.Profile, ct);
                    var clinical = await medical.ScopeAsync(child.Patient.Id, ct);
                    var contact = await profiles.CanManageAsync(child.Patient.Id, FamilyExtensionPermissionNames.ManageContacts, ct);
                    var photo = await profiles.CanManageAsync(child.Patient.Id, FamilyExtensionPermissionNames.ManagePhoto, ct);
                    var finance = await dependent.AuthorizeAsync(child.Patient.Id, FamilyExtensionPermissionNames.Receipts, ct);
                    var upload = historical.Value.Enabled && (await medicalOperations.ScopeAsync(child.Patient.Id, MedicalDocumentPermissionNames.CreateDependent, ct)).IsSuccess;
                    var diagnostic = (await medicalOperations.ScopeAsync(child.Patient.Id, DependentDiagnosticPermissionNames.LabCreate, ct)).IsSuccess ||
                        (await medicalOperations.ScopeAsync(child.Patient.Id, DependentDiagnosticPermissionNames.RadiologyCreate, ct)).IsSuccess;
                    var status = await medical.StatusAsync(child.Patient.Id, ct);
                    var correct = (await dependent.AuthorizeAsync(child.Patient.Id, FamilyGovernancePermissionNames.IdentityCorrectionRequest, ct)).IsSuccess &&
                        member.Role is FamilyMemberRole.Father or FamilyMemberRole.Mother;
                    var proof = await proofReader.FindAsync(actor.Value.Id, child.Patient.Id, ct);
                    var under18 = child.Patient.DateOfBirth > options.Value.Today(clock.UtcNow).AddYears(-18);
                    string? OperationalReason(string permission, bool allowed) => allowed ? null : !Has(permission) ? "DependentAccess.PermissionRequired" :
                        !under18 ? "DependentAccess.AdultChild" : aggregate.Status != FamilyStatus.Active ? "DependentAccess.FamilyInactive" :
                        proof is null ? "DependentAccess.OperationalProofRequired" : "DependentAccess.Unavailable";
                    var medicalReason = clinical.IsFailure ? status.IsSuccess ? status.Value.UnavailableReasonCode : "DependentAccess.MedicalPolicyUnavailable" : null;
                    var canVerify = under18 && aggregate.Status == FamilyStatus.Active && proof is null && Has(FamilySelfServicePermissionNames.Submit) && member.Role is FamilyMemberRole.Father or FamilyMemberRole.Mother;
                    var reasons = new Dictionary<string, string?>
                    {
                        ["canBook"] = OperationalReason(PermissionNames.ReservationsCreateDependents, book.IsSuccess),
                        ["canViewReservations"] = OperationalReason(PermissionNames.ReservationsViewDependents, view.IsSuccess),
                        ["canViewQueue"] = OperationalReason(FamilySelfServicePermissionNames.Tickets, queue.IsSuccess),
                        ["canViewProfile"] = OperationalReason(FamilySelfServicePermissionNames.Profile, profile.IsSuccess),
                        ["canManageContacts"] = OperationalReason(FamilyExtensionPermissionNames.ManageContacts, contact),
                        ["canManagePhoto"] = OperationalReason(FamilyExtensionPermissionNames.ManagePhoto, photo),
                        ["canCorrectIdentity"] = OperationalReason(FamilyGovernancePermissionNames.IdentityCorrectionRequest, correct),
                        ["canViewReceipts"] = OperationalReason(FamilyExtensionPermissionNames.Receipts, finance.IsSuccess),
                        ["canViewMedicalRecord"] = medicalReason,
                        ["canUploadHistoricalDocument"] = upload ? null : !historical.Value.Enabled ? "HistoricalDocument.Disabled" : "DependentAccess.MedicalWriteUnavailable",
                        ["canSubmitDoctorOrderedDiagnostic"] = diagnostic ? null : "DependentAccess.MedicalWriteUnavailable",
                        ["canRequestVerification"] = canVerify ? null : proof is not null ? "DependentAccess.AlreadyVerified" : "DependentAccess.Unavailable"
                    };
                    children.Add(new(child.Patient.Id, child.Patient.NameAr, book.IsSuccess, view.IsSuccess, queue.IsSuccess, clinical.IsSuccess,
                        profile.IsSuccess, contact, upload, finance.IsSuccess, reasons["canBook"], medicalReason,
                        photo, diagnostic, canVerify, correct, reasons));
                }
            }
        }

        var canManage = family is null || family.Status == FamilyStatus.Active && (member!.Role is FamilyMemberRole.Father or FamilyMemberRole.Mother);
        return Result<FamilyWorkspaceResponse>.Ok(new(family is not null, family, children, pending.Items,
            canManage && Has(FamilySelfServicePermissionNames.Search) && Has(FamilySelfServicePermissionNames.Submit),
            canManage && Has(FamilySelfServicePermissionNames.Onboard), family is null && Has(FamilySelfServicePermissionNames.Submit)));
    }
}

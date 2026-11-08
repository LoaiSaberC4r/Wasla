# PR #38 current frontend handoff — 2026-11-08

All routes below start with `/api/v1`. Enums use string JSON. Existing field names
and routes are retained. These are the current additions to API-HANDOFF.md and
API-HANDOFF-EXTENSIONS.md; historical checkpoint availability statements are
superseded. Flags being enabled never grants access. JWT and live DB permissions
must both permit the operation. Sensitive responses use private/no-store delivery.

## Changed contracts

| Method / route | Permission / scope | Request | Response / behavior |
|---|---|---|---|
| GET /families/mine/workspace | FamilyWorkspace.ViewOwn; active linked adult Patient | none | FamilyWorkspaceResponse, 200 with hasFamily=false/family=null for empty state; GET /families/mine still returns historical 404 |
| POST /families/patient-candidates/search | PatientFamilyCandidates.Search; active linked adult | FamilyCandidateSearch: nameAr + dateOfBirth + gender required, phoneNumber optional | FamilyCandidateSearchResponse: searchStatus, candidates, hasMore, new nextAction; 200 zero/one/multiple, 429 limit |
| POST /family-relationship-requests/from-candidate | PatientFamilyCandidates.Submit; requester-bound opaque selection | multipart candidateReference, relationshipType Father/Mother, evidenceFiles + matching documentTypes, Idempotency-Key | 202 FamilyCandidateSubmissionResponse: requestId OR conflictCaseId, status, nextAction; parent-role conflict409, expired/wrong-actor/stale-identity reference404 |
| POST /families/mine/child-onboarding | ChildOnboarding.CreateOwn; adult applicant | ChildOnboardingForm identity/contact/relationship/evidence fields + key | 202 ChildOnboardingResponse; provisional intake only |
| POST /families/mine/child-onboarding-requests/{requestId}/resubmit | ChildOnboarding.ResubmitOwn; original applicant | ChildOnboardingResubmissionForm: rowVersion, evidence; optional complete revised identity/contact block; key | 200 same request, revision++; both child and guardian phone missing422; stale version/key mismatch409 |
| POST /admin/child-onboarding-requests/{requestId}/approve | ChildOnboarding.Decide and existing family approval permission; SuperAdmin | ChildOnboardingDecision: rowVersion, confirmedExistingPatientId?; key | 200 ChildOnboardingResponse; cross-family status=ConflictReview, new conflictCaseId and nextAction=WaitForConfidentialReview; no approvedPatientId in conflict |
| GET /admin/child-onboarding-requests/{requestId} | ChildOnboarding.ViewDetails; SuperAdmin | optional matchPageNumber (default1) and matchPageSize (default20, max100) | ChildOnboardingReview adds hasMorePossibleMatches and possibleMatchPageNumber; stable ordered duplicate matches can be paged before explicit identity selection |
| PATCH /families/mine/dependents/{childPatientId}/phone | DependentPatientProfile.ManageContacts; live exact under18 operational pair; child has no linked account | new DependentPhoneForm: rowVersion, phoneNumber nullable; Idempotency-Key | 200 DependentProfileChangeResponse {changeId,contactId?}; invalid422, no fallback primary contact409, stale409 |
| GET /reservations/mine/{reservationId} | Reservations.ViewOwn or Reservations.ViewDependents plus exact current subject authority | reservationId | ReservationDetailsResponse; parent linkedTicketId appears only when live dependent ticket access and archive rules permit; otherwise null/canViewLinkedTicket=false |
| POST /patients/me/historical-documents and /families/mine/dependents/{childPatientId}/historical-documents | MedicalDocuments.CreateOwn / CreateDependent; own adult or exact medical under16 Father/Mother | HistoricalDocumentForm category/date/provider/title/note/files + key | clean201; rejected/quarantined202; HistoricalDocumentMutationResponse now adds validationState and failureCode |
| POST either historical prefix/{documentId}/versions | MedicalDocuments.ReviseOwn / ReviseDependent; same scope, proxy uploader-owned | same form + rowVersion/reason/key | clean200; failed validation202 without replacing current valid version; same mutation response |
| GET either historical prefix[/{documentId}] | corresponding MedicalDocuments.ViewOwn/ViewDependent; dependent also needs MedicalRecords.ViewDependent | pageNumber/pageSize/includeWithdrawn | HistoricalDocumentResponse now includes rejected/quarantined source status; currentVersion is null for never-published sources, canRevise/canWithdraw=false |

Historical mutation receipts always remain source=PatientProvided and
verificationStatus=Unverified. validationState is AvailableUnverified,
RejectedUnsafe or Quarantined. A202 receipt acknowledges a rejected attempt,
not an available medical file. failureCode is MalwareDetected or
ScannerUnavailable (size-limit rejection may be ScannerSizeLimit). No unsafe
attachment is published. A failed revision retains the previous current version.
A missing real scanner registration still returns409 HistoricalDocument.ScannerUnavailable.
Downloads require AvailableUnverified, exact owner/child scope and fresh authority;
withdrawn/rejected/quarantined sources have no download link. Do not render them
as clinical results or include them in official result counters.

## Workspace capability and pending-state contract

Each FamilyWorkspaceChild keeps existing fields and now independently adds:
canCancelReservation, canRescheduleReservation, canViewTicket,
canViewHistoricalDocuments, canRequestIdentityCorrection, canSubmitLabResult,
canSubmitRadiologyResult, canViewLabSubmissions, canViewRadiologySubmissions,
canWithdrawLabSubmission, canWithdrawRadiologySubmission, canViewTransactions.
canViewDiagnosticSubmissions/canWithdrawDiagnosticSubmission and the existing
canSubmitDoctorOrderedDiagnostic indicate that at least one corresponding action
is available; choose the specific lab/radiology Boolean when rendering its action.
canCorrectIdentity remains an alias of canRequestIdentityCorrection.

Keep booking/view/cancel/reschedule permissions independent. Keep transactions
and receipts independent. A clinical denial does not hide authorized operational
actions. Contact/photo editing remains unavailable on a linked child's account.
All flags advertise scope eligibility; reservation status, slot capacity, order
ownership, clinical encounter state, uploader ownership and concurrency are
evaluated again at the actual action endpoint.

capabilityReasons contains each can* key, null when allowed. Operational reasons:
DependentAccess.PermissionRequired, AdultChild, FamilyInactive,
OperationalProofRequired, AccountLinked, AlreadyVerified, Unavailable.
Medical reasons preserve existing PolicyUnavailable, RestrictionActive,
AgeNotEligible, RelationshipNotEligible, ParentChildVerificationRequired,
EligibilityReviewPending, EligibilityUnavailable, PermissionUnavailable and
explicit DependentAccess.MedicalPolicyUnavailable/MedicalWriteUnavailable fallbacks.
HistoricalDocument.Disabled represents a non-Production configuration with that
feature off. All Production flags are required to be on at startup.

New pendingWorkflows is a bounded list of FamilyWorkspacePendingItem:
kind, reference, status, nextAction, createdAtUtc. Kinds include ChildOnboarding,
MembershipConflict, IdentityCorrection and SecondParentInvitation. Display
PendingReview, ModificationRequested, ConflictReview, PendingGovernanceDecision
and PendingRedemption using their next actions. Expired/revoked/consumed
invitations are excluded. Existing pendingRequests retains relationship requests,
including reviewed second-parent joining. No other-family roster or private
reviewer note is added. Opening a confidential review does not require managing
the applicant's current Family and does not grant another Family's access.

Search candidates contain only candidateReference, displayNameMasked, birthYear
and expiresAtUtc. Never request/display raw target PatientId, FamilyId, full birth
date, phone, email or account-link status. Phone ranks exact three-field matches;
it never excludes or broadens them. Ambiguous bounded candidates still require
evidence/review. When multiple matches or no match exist, nextAction points to child
onboarding for explicit identity review; approval reuses a matching existing
Patient and does not create a duplicate. Never imply candidate selection grants
membership or access.

## Complete journey route map

| Journey | Existing routes / DTOs | Scope and frontend action |
|---|---|---|
| Family Workspace / empty state | GET /families/mine/workspace; FamilyWorkspaceResponse | Open directly; do not require GET mine to succeed first |
| Discovery / existing child linking | candidate search + from-candidate above | Store opaque reference only; status tracking uses own relationship request routes |
| Onboarding | POST /families/mine/child-onboarding; GET /families/mine/child-onboarding-requests[/{id}]; resubmit above | Pending intake creates no Patient; conflictCaseId navigates to own governance status |
| Second parent | POST /families/mine/join-invitations; POST /families/mine/join-requests; POST /families/mine/join-invitations/revoke | FamilyJoinInvitationResponse / FamilyCandidateSubmissionResponse; FamilyJoinInvitations.CreateOwn/RedeemOwn; private30-minute reference, evidence, key; no automatic parent grant |
| Family conflict | GET /families/mine/governance-cases[/{caseId}]; POST .../{caseId}/resubmit-conflict | FamilyGovernancePage / FamilyGovernanceReceipt; own applicant only; evidence+rowVersion+key for resubmit |
| Identity correction | POST /families/mine/dependents/{childPatientId}/identity-corrections | DependentIdentityCorrectionInput plus reason/evidence/key, optional caseId/rowVersion revision; DependentIdentityCorrection.RequestOwn; exact current parent authority; review retains identity/history |
| Governance review | GET /admin/family-governance-cases[/{caseId}], private evidence; POST approve/reject/request-modification/refer-governance | Existing authorized SuperAdmin family-review permissions; evidence and revision history; rowVersion/key; conflict approval refers to governance, never transfers a child |
| Profile / contacts | GET /families/mine/dependents/{childPatientId}/profile; GET contacts; POST contacts; PUT/DELETE contacts/{contactId} | DependentProfileResponse / PatientContactInput / DependentProfileChangeResponse; granular read/contact permissions; identity-critical fields absent from routine write DTO |
| Profile photo | GET profile-image; PATCH profile (multipart Photo,RowVersion,key) | DependentPatientProfile.ManagePhoto; exact pair/no child account; JPEG/PNG; previous valid image retained through commit failure |
| Reservations | GET /reservations/bookable-patients; POST /reservations; GET /reservations/mine[/{id}]; POST .../{id}/cancel and /reschedule; existing availability routes | Existing reservation DTOs; independent create/view/cancel/reschedule dependent permissions; exact under18 authority; normal pricing/slot/concurrency rules |
| Follow-up eligibility | GET /follow-up-eligibilities/mine?patientId={child}; POST reservation with followUpEligibilityId | Existing FollowUp permission and bookable-child scope; no medical sharing prerequisite |
| Ticket navigation | GET /tickets/dependents/{childPatientId}/active; GET /tickets/dependents/{childPatientId}/{ticketId} | FamilyDependentTickets.View; DependentTicketResponse; terminal archive also needs Archives.Tickets.ViewOwn; no queue operation controls |
| Medical History | GET /families/mine/dependents/{childPatientId}/medical-record/... | Existing dependent Medical Record DTOs and permission; exact medical proof/policy, under16, restrictions/audit; doctor routes retain treating/visibility/subscription checks |
| Historical sources | own/dependent historical prefixes above, withdrawal and private attachment content | Separate create/view/revise/withdraw permissions; logical withdrawal and immutable versions; historical Timeline type=HistoricalPatientDocument |
| Lab/Radiology submissions | /families/mine/dependents/{childPatientId}/lab-requests/{requestId}/submissions and radiology equivalent; existing view/withdraw routes in extension handoff | Independent DependentLabSubmissions.* / DependentRadiologySubmissions.*; doctor order/child/encounter and exact under16 medical operation scope |
| Dependent finance | GET /families/mine/dependents/{childPatientId}/financial-transactions; payments/{paymentId}/receipt; refunds/{refundId}/receipt | FinancialPage<PatientFinancialTransactionItem>, PaymentReceiptData/RefundReceiptData; independent read permissions; forced subject; no internal notes/refund reason or payment mutation |

Standard errors:401 unauthenticated,403 missing endpoint permission,404 private
resource/scope unavailable,409 stale rowVersion/idempotency mismatch/role or
state conflict,422 business validation,429 rate limit. Endpoint-specific existing
booking/clinical/finance errors remain documented in the previous handoffs.
Idempotent mutations require the same actor, key and payload; same-key different
payload409. Candidate and invitation tokens are one-use; an exact durable retry
returns its original request/case receipt. RowVersion is an opaque Base64 SQL
rowversion, never a client counter. Re-read the resource after a409. Read-only
queries have no idempotency key and never authorize future writes. Relation,
permission or policy revocation affects the next request immediately.

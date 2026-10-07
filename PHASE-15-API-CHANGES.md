# Phase 15 API changes — 2026-10-07

All routes use `/api/v1`, bearer authentication and the existing localized ProblemDetails contract. JSON uses camelCase properties and string enums. Dates use ISO 8601; `externalReportDate` is a date (`YYYY-MM-DD`). Tokens are opaque base64 strings. GUIDs identify resources, never authorization scope by themselves. Swagger is available at `/swagger/v1/swagger.json` in Development.

## Actors and permissions

`MedicalCatalogManager` is a distinct account type/role, with role ID `10000000-0000-0000-0000-000000000006`. Root creates it with an initial strong password; the manager must change that password before using reference-data routes. Only the seeded Root SuperAdmin has manager-account governance. Ordinary SuperAdmin, Reception, DrugCatalogManager and MedicalCatalogManager have no diagnostic clinical override.

The 62 new distinct permission names are appended to the deterministic permission registry. Default mappings are manager 20, Doctor 28, Patient 10 and Root governance 6; two submission-view permissions are shared between Doctor and Patient. For each `K` in `Lab`, `Radiology`:

| Actor | Permissions |
| --- | --- |
| MedicalCatalogManager | `KCatalog.View`, `.Create`, `.Update`, `.Activate`, `.Deactivate`, `.Merge`, `.Import`, `.ImportHistory`; `KCatalogRequests.View`, `.Review` |
| Doctor | `KCatalog.SearchActive`; `KCatalogRequests.CreateOwn`, `.ViewOwn`, `.UpdateOwn`; `KRequests.ViewOwn`, `.ManageOwnDraft`, `.CreatePostVisitOwn`, `.CancelOwn`; `KResults.ViewOwn`, `.UploadOwn`, `.CorrectOwn`, `.VoidOwn`; `KResultSubmissions.ViewOwn`, `.ReviewOwn` |
| Patient | `KRequests.ViewOwnIssued`; `KResults.ViewOwnCurrent`; `KResultSubmissions.CreateOwn`, `.ViewOwn`, `.WithdrawOwn` |
| Root | `MedicalCatalogManagers.ViewAll`, `.ViewDetails`, `.Create`, `.Update`, `.Activate`, `.Deactivate` |

Every clinical action also checks current account activity, completed first login, current database permissions, Doctor approval, requesting-Doctor ownership and active owned Practice, or the Patient's account link and ownership. A valid older JWT cannot bypass revocation. Another Doctor/Patient receives a safe 404. Suspended Doctors cannot review or download; Patients retain their own issued history. No family relationship grants additional clinical scope.

## Request bodies

Unless the route is multipart, send `application/json`. Properties marked optional may be omitted or null.

| Body | Fields |
| --- | --- |
| Create manager | `userName`, `email`, optional `phoneNumber`, `initialPassword`, `confirmPassword` |
| Update manager | `email`, optional `phoneNumber` |
| Catalog presentation | `displayNameEn`, `displayNameAr`, `aliasesEn`, `aliasesAr`, `internalNote`; English display name is required for a Wasla-created row |
| Update catalog | `data`: presentation object; `rowVersion` |
| Catalog lifecycle/merge | `rowVersion`, nonblank `reason`; merge also requires `targetCatalogId` |
| Lab missing test | `testName`, optional `specimen`, `catalogClarificationNote` |
| Radiology missing procedure | `procedureName`, optional `specimen`, `catalogClarificationNote` |
| Update own catalog request | `data`: the appropriate missing-test/procedure object; `rowVersion` |
| Review catalog request | `rowVersion`, optional `reason`, `canonicalCatalogId`, `approvedData`: presentation; more-info/reject require a reason; approve links an active canonical catalog row or creates a Wasla row from the reviewed presentation (defaults to the submitted name if omitted); reject with an active canonical ID records a Duplicate reason |
| Add Lab item | exactly one of `labTestCatalogId` / `newLabTest`: Lab missing-test object; optional `doctorInstructions`, `patientInstructions`, `labRequestRowVersion` |
| Add Radiology item | exactly one of `radiologyProcedureCatalogId` / `newRadiologyProcedure`: Radiology missing-procedure object; optional `doctorInstructions`, `patientInstructions`, `radiologyRequestRowVersion` |
| Update draft item | `doctorInstructions` (optional), `labRequestRowVersion` or `radiologyRequestRowVersion`; source and snapshots cannot be replaced |
| Remove draft item | no body; required query `rowVersion` |
| Post-visit order | nonblank `postVisitReason`, optional `patientInstructions`, nonempty `items` array of domain-specific item objects (catalog ID XOR new missing-test/procedure, optional `doctorInstructions`) |
| Cancel item/remaining | `rowVersion`, nonblank `reason` (Patient-visible) |
| Accept Lab submission | `coveredLabRequestItemIds`: nonempty unique GUID array; `rowVersion` of submission |
| Accept Radiology submission | `coveredRadiologyRequestItemIds`: nonempty unique GUID array; `rowVersion` of submission |
| Reject submission | `rowVersion`, nonblank `patientVisibleReason` |
| Withdraw submission | `rowVersion` of submission |
| Void official result | `rowVersion` of result, nonblank internal `reason` |
| Apply import | `rowVersion` of batch, optional `skipPossibleConflicts` (default false) |
| Discard import | `rowVersion` of batch |

Names allow 1,000 characters, aliases 4,000 each, specimen/provider 500, clarification/internal/patient/doctor instructions 2,000, reasons 1,000. The combined post-visit item limit is 100 per command. Official LOINC fields cannot be changed through presentation APIs. Review metadata must not contain Patient/Encounter context; order-specific instructions are sent separately from the missing catalog description.

### Multipart medical documents

Send repeated `attachments` fields with PDF, JPG/JPEG or PNG files. A file must have matching extension, declared MIME and signature. BuildingBlock media validation and its configured malware-scanner chain apply before private storage. Limits are **10 MiB per file, 20 files, 100 MiB total**. Radiology requires one repeated `attachmentKinds` field per attachment, in matching order, containing `Report` or `Image`. Lab attachments default to Report. DICOM, SVG, HTML and other types are excluded.

Doctor direct upload fields:

```text
attachments=<binary file>, repeated
attachmentKinds=Report|Image, repeated and required for Radiology
coveredLabRequestItemIds=<guid>, repeated (Lab)
coveredRadiologyRequestItemIds=<guid>, repeated (Radiology)
externalLaboratoryName=<optional> (Lab)
externalRadiologyCenterName=<optional> (Radiology)
externalReportDate=<optional YYYY-MM-DD>
rowVersion=<current request token for direct upload>
```

Correction uses the same format, with a **result** token in `rowVersion`, mandatory nonblank `reason`, new attachments and the complete new coverage selection. Coverage may retain items completed by this result's prior current version; it cannot claim another result's completed items, cancelled items or items from another request.

Patient submission fields are `attachments`, Radiology `attachmentKinds`, the appropriate optional external-provider field, optional `externalReportDate`, and optional `patientNote`. **There is no coverage field in the Patient schema.** Supplying any form field containing `covered` is rejected with 422. Patient upload creates a PendingReview submission and changes no official result, item or order state.

The ZIP import is a separate `multipart/form-data` contract: `file` (one `.zip`) and `sourceVersion` (default `2.83`). Its default compressed package limit is 200 MiB and is configured independently under `DiagnosticCatalogImport`. Normal media limits do not apply to catalog packages. Expected files are `LoincTable/Loinc.csv`, `LoincUniversalLabOrdersValueSet.csv` for Lab, `LoincRsnaRadiologyPlaybook.csv` for Radiology, and optional `arJO32LinguisticVariant.csv`; accessory/linguistic CSVs are found by unique basename in safe ZIP paths. Missing/ambiguous files or unsupported schemas fail validation. Packages are parsed offline, staged for review and never added to Patient storage.

## Responses and capabilities

Paged responses are `{ items, totalCount, pageNumber, pageSize }`; page numbers clamp to 1–1,000,000 and page size to 1–100 (default 20). No list fetches the full runtime clinical/catalog dataset into application memory.

| Response DTO | Fields |
| --- | --- |
| `MedicalCatalogManagerResponse` | `id`, `userName`, `email`, `phoneNumber`, `isActive`, `isFirstLogin`, `createdOnUtc` |
| `MedicalCatalogResponse` | `catalogId`, `source`, `status`, `loincCode`, resolved `nameEn/nameAr`, `officialNameEn/officialNameAr`, `externalStatus`, `sourceVersion`, `isCommonOrder`, `presentation`, `sourceDataJson`, `attributesJson`, `mergedIntoId`, `rowVersion`, `capabilities`, optional `history`; Doctor search excludes internal notes and source JSON |
| `MedicalCatalogRequestResponse` | `requestId`, `requestedByDoctorId`, `name`, `specimen`, `catalogClarificationNote`, `status`, `canonicalCatalogId`, `reasonType`, `reviewReason`, `createdAtUtc`, `rowVersion`, `capabilities`, `history`, optional `canonicalCatalog` |
| `DiagnosticRequestSummary` | `requestId`, `medicalEncounterId`, `patientId`, `practiceId`, `origin`, `status`, `requestedAtUtc`, `rowVersion` |
| `DiagnosticRequestStateResponse` | `requestId`, `medicalEncounterId`, `doctor/practice`: `{id,nameAr,nameEn}`, `origin`, `status`, `patientInstructions`, `requestedAtUtc`, `rowVersion`, `items`, `capabilities`, `currentResults`, `submissions`; internal `postVisitReason` only for Doctor |
| `DiagnosticItemResponse` | `itemId`, `nameArSnapshot`, `nameEnSnapshot`, `loincCodeSnapshot`, optional `modalitySnapshot/anatomicLocationSnapshot/lateralitySnapshot`, `doctorInstructions`, `status`, `cancellationReason`, `capabilities`; Doctor-only `catalogId/catalogRequestId` |
| `DiagnosticResultResponse` | `resultId`, `requestId`, `currentVersionNumber`, `current`: valid finalized version or null, `rowVersion`, `capabilities` |
| `DiagnosticVersionResponse` | `versionId`, `versionNumber`, `status`, `externalProviderName`, `externalReportDate`, `originallyUploadedBy`, `originallyUploadedAtUtc`, `reviewedByDoctorId`, `reviewedAtUtc`, `coveredItemIds`, `attachments`; Doctor-only `correctionReason/voidReason` |
| `DiagnosticAttachmentResponse` | `attachmentId`, `originalFileName`, `contentType`, `sizeBytes`, `sha256`, `uploadedAtUtc`, `kind`; never a storage key, public URL or bytes |
| `DiagnosticSubmissionSummary` | `submissionId`, `requestId`, `status`, `submittedAtUtc` |
| `DiagnosticSubmissionResponse` | `submissionId`, `requestId`, `status`, `externalProviderName`, `externalReportDate`, `patientNote`, `patientVisibleReason`, `acceptedResultId`, `submittedAtUtc`, `rowVersion`, `attachments`, `capabilities` |
| `DiagnosticResultMutationResponse` | `submission` (nullable), `result` (nullable), `request`: latest request state; nested output is limited by current view permissions |
| `DiagnosticImportBatchResponse` | `batchId`, `sourceVersion`, `originalFileName`, `fileSha256`, `uploadedByUserId`, `uploadedAtUtc`, `status`, `totalRecords`, `counts` by disposition, `appliedByUserId/appliedAtUtc`, `rowVersion`, `capabilities` |
| `DiagnosticImportRecordResponse` | `recordId`, `loincCode`, `nameEn`, `disposition`, `sourceDataJson`, `matchedCatalogId` |
| `DiagnosticHistoryResponse` | `id`, `action`, `reason`, `beforeSnapshot`, `afterSnapshot`, `actorUserId`, `actorType`, `occurredAtUtc`; only on authorized Doctor/reference-data history routes |

Every draft mutation returns the complete replacement state. `patientInstructions` is set when the root is first created (initial add or post-visit creation); subsequent item edits change only DoctorInstructions. Removing the last item removes the empty unissued root and returns a 200 state with `requestId/status/rowVersion` null and empty items. It retains the catalog review request and immutable removal history. Issued orders and clinical history cannot be deleted.

Capabilities are derived from actor, current permissions and state:

- Catalog: `canEdit`, `canActivate`, `canDeactivate`, `canMerge`.
- Catalog request: `canEdit`, `canRequestMoreInfo`, `canApprove`, `canReject`.
- Order: `canManageDraft`, `canCancelRemaining`, `canUploadResult`.
- Item: `canEdit`, `canRemove`, `canCancel`, `hasCurrentResult`.
- Result: `canCorrect`, `canVoid`.
- Submission: `canWithdraw`, `canAccept`, `canReject`.
- Import: `canApply`, `canDiscard`.

Capabilities guide the UI; every write/download independently reauthorizes. An action does not grant a revoked view permission. Nested results/submission summaries are omitted when their view permission is absent. Diagnostic Doctor/Patient routes emit `Cache-Control: no-store` and are not globally query-cached.

`source` is `Loinc` or `Wasla`; `originallyUploadedBy` is `Doctor` or `Patient`. Import status is `Staged`, `Applied` or `Discarded`; disposition/count keys are `New`, `Unchanged`, `Changed`, `ArabicAdded`, `ArabicChanged`, `ExternalStatusChanged`, `PossibleConflict` or `Skipped`. Applied batches retain immutable PossibleConflict rows when the manager explicitly skips them. These rows remain visible for later review; apply does not silently reclassify or overwrite them.

## Lifecycles and visibility

Catalog: `Active`, `Inactive`, `Merged`; imported rows are selectable only when their canonical external status is `ACTIVE`. Merge targets must be active canonical rows, and incoming chains are flattened. Historical order snapshots retain their original identity and terminology.

Catalog request: `Pending → NeedsMoreInfo → Pending` on Doctor resubmission; `Pending → Approved|Rejected`. Approval/rejection never rewrites an existing order item. Duplicate rejection can return the existing active canonical row.

Order origin: `DuringEncounter` or `PostVisit`. Status: `Draft`, `Requested`, `PartiallyCompleted`, `Completed`, `Cancelled`. Item status: `Requested`, `Completed`, `Cancelled`. There is at most one draft per encounter/domain. Complete Visit publishes it; post-visit creates a new Requested order under the same completed encounter without changing the encounter or creating an amendment.

Cancel only Requested items with a Patient-visible reason. Cancel-remaining preserves Completed items: when no Requested items remain, the order is Completed if any item is Completed, otherwise Cancelled. Remaining PendingReview submissions become NoLongerApplicable when cancellation leaves no requestable item.

Submission: `PendingReview → Accepted|Rejected|Withdrawn|NoLongerApplicable`; terminal states cannot be edited. The requesting Doctor alone accepts/rejects. Acceptance creates one Official Result covering the selected items and reuses the privately stored source files with Patient uploader provenance; no PDF is duplicated per item. Suspension leaves pending submissions pending and grants no substitute reviewer.

Result version: `Finalized → Superseded` on successful correction, or `Finalized → Voided`. Correction is one operation, with no correction draft. Voiding reopens uncovered Completed items to Requested and never reopens the encounter. Patients see issued orders and current valid official results immediately. Patient result GET/download returns 404 after void or when requesting a superseded official attachment. Patients retain their own original submission documents through the separate submission routes; those are not presented as current official attachments. Internal post-visit/correction/void reasons, catalog request/source IDs and clinical audit data are absent from Patient output.

## Filters, sorting and retry

Catalog list/search: `search`, `source`, `hasArabic`, `commonOnly`, pagination; manager also `status`. Search normalizes English/Arabic names, aliases, component, short name and LOINC code. Doctor search only returns selectable canonical rows, including canonical matches from merged names/codes. Common Lab orders rank first; exact code and deterministic name/ID sorting follow.

Catalog requests: `status`, pagination; manager also `doctorId`, `search`. Requests: `practiceId`, `patientId`, `encounterId`, `origin`, `status`, `fromUtc`, `toUtc`, pagination. Results: `practiceId`, `patientId`, `requestId`, date range and pagination. Doctor submission inbox: `status` (or `submissionStatus`), `practiceId`, `patientId`, `requestId`, date range and pagination; default PendingReview and oldest submission first. Patient request-submission list accepts pagination. Imports: pagination; changes also `disposition`, `search`. Version/history arrays are ordered and are not paged. Clinical date upper bounds are inclusive in these new list APIs.

`rowVersion` is required when editing an existing root or changing its state. First add has no order token; subsequent adds use the returned domain-specific token. Direct upload uses the request token; review/withdraw use the submission token; correction/void use the result token. Child mutations advance root versions. A stale, missing required or extraneous completion token returns 409. Refresh the authorized state and rebase the user's intended edit; do not silently overwrite.

`Idempotency-Key` is required for merge, import apply, catalog-request approve, draft add, post-visit create, direct official upload, Patient submission, acceptance, correction and void. It is scoped to actor + operation, limited to 200 characters, and must be preserved with the identical payload (including files and tokens) for an uncertain network retry. Matching retries create no duplicate work; clinical retries return latest authorized state. Reuse with a changed payload returns 409. Failure retains no success key. Cancellation, rejection, withdrawal, presentation/lifecycle updates, discard and account operations do not require it.

Replace frontend local state from every successful mutation. On a timeout, retry the identical command/key first; on a confirmed validation failure, fix inputs and send a new logical command/key. On 409 concurrency, refresh state before a new logical edit/key. Do not generate a new key merely because a response was lost. On 403 re-evaluate current permissions/account state. Never construct media URLs from storage keys: use the explicit content routes with a bearer token. Download responses recheck current ownership/version validity and stream the validated MIME and original filename.

## Changes to existing routes

`GET /doctors/me/practices/{practiceId}/encounters/{encounterId}` and `GET /doctors/me/practices/{practiceId}/tickets/{ticketId}/encounter` now include nullable `labRequestDraft`, `radiologyRequestDraft`, `labRequestsSummary`, `radiologyRequestsSummary`, and capabilities `canManageLabRequest`, `canManageRadiologyRequest`, `canCreatePostVisitLabRequest`, `canCreatePostVisitRadiologyRequest`. Draft fields and summaries require the respective view permission. Summaries contain the first 100 orders per domain; use the paged request endpoint filtered by encounterId for further orders. Existing Patient encounter DTOs retain their Phase 13 shape.

`POST /doctors/me/practices/{practiceId}/tickets/{ticketId}/complete` adds optional `labRequestRowVersion` and `radiologyRequestRowVersion` alongside existing ticket, encounter and prescription tokens. Each diagnostic token becomes mandatory if that domain has a draft. All prescription and diagnostic validation happens before transition. Ticket completion, encounter completion, prescription finalization, Lab/Radiology publication, follow-up and their histories/idempotency commit in one transaction. No diagnostic item is inserted into Ticket, reservation or financial DTOs. Start Visit and the Phase 14 prescription correction workflow retain their behavior.

## Errors

Standard status mapping: 400 malformed transport input; 401 unauthenticated; 403 actor/account/permission denial; 404 inaccessible/missing scoped resource; 409 state/concurrency/idempotency conflict; 422 validation; 500 unexpected storage/persistence failure. Server/proxy transport body ceilings can produce 413 before application handling; the configured import-size check itself returns localized 422 `DiagnosticCatalogImport.PackageTooLarge`. ProblemDetails includes `errors[].code/message/type`, correlation/trace identifiers and the existing localization behavior.

Common actionable codes include `Diagnostics.AccessDenied`, domain-specific `*Request.ConcurrencyConflict`, `*Result.ConcurrencyConflict`, `*ResultSubmission.NotPending`, `*Result.InvalidCoverage`, `*Result.AttachmentRequired`, `*Catalog.NotActive`, `*Catalog.InvalidMergeTarget`, `*CatalogRequest.InvalidState`, and import safety/schema/conflict codes. Existing BuildingBlock signature/MIME/scanner failures retain `Media.InvalidFile`, `Media.FileTooLarge`, `Media.UnsupportedType`, `Media.SignatureMismatch`, `Media.MalwareDetected`, `Media.StorageUnavailable` as applicable. The reused idempotency helper retains `Medication.IdempotencyKeyRequired` and `Medication.IdempotencyKeyReused`; new diagnostic operations use distinct operation namespaces.

The route inventory and complete localized diagnostic code inventory follow. Some localized codes reserve more specific future error refinements; callers should use the codes actually returned, and capability/state data, rather than English messages.

## New route inventory (118)

Each row requires the stated permission plus the actor/state/ownership checks above. Successful JSON responses are 200 except catalog/account creation (201). File routes return 200 with the validated document MIME.

### Root accounts (6)

| Actor | Method | Route | Permission | Response / key |
| --- | --- | --- | --- | --- |
| Root | GET | `/api/v1/admin/medical-catalog-managers` | `MedicalCatalogManagers.ViewAll` | page of MedicalCatalogManagerResponse |
| Root | GET | `/api/v1/admin/medical-catalog-managers/{id}` | `MedicalCatalogManagers.ViewDetails` | MedicalCatalogManagerResponse |
| Root | POST | `/api/v1/admin/medical-catalog-managers` | `MedicalCatalogManagers.Create` | MedicalCatalogManagerResponse |
| Root | PUT | `/api/v1/admin/medical-catalog-managers/{id}` | `MedicalCatalogManagers.Update` | MedicalCatalogManagerResponse |
| Root | POST | `/api/v1/admin/medical-catalog-managers/{id}/activate` | `MedicalCatalogManagers.Activate` | MedicalCatalogManagerResponse |
| Root | POST | `/api/v1/admin/medical-catalog-managers/{id}/deactivate` | `MedicalCatalogManagers.Deactivate` | MedicalCatalogManagerResponse |

### Lab (56)

| Actor | Method | Route | Permission | Response / key |
| --- | --- | --- | --- | --- |
| Manager | GET | `/api/v1/admin/lab-catalog` | `LabCatalog.View` | page of MedicalCatalogResponse |
| Manager | GET | `/api/v1/admin/lab-catalog/{id}` | `LabCatalog.View` | MedicalCatalogResponse |
| Manager | POST | `/api/v1/admin/lab-catalog` | `LabCatalog.Create` | MedicalCatalogResponse |
| Manager | PUT | `/api/v1/admin/lab-catalog/{id}` | `LabCatalog.Update` | MedicalCatalogResponse |
| Manager | POST | `/api/v1/admin/lab-catalog/{id}/activate` | `LabCatalog.Activate` | MedicalCatalogResponse |
| Manager | POST | `/api/v1/admin/lab-catalog/{id}/deactivate` | `LabCatalog.Deactivate` | MedicalCatalogResponse |
| Manager | POST | `/api/v1/admin/lab-catalog/{id}/merge` | `LabCatalog.Merge` | MedicalCatalogResponse; key required |
| Manager | POST | `/api/v1/admin/lab-catalog/imports/preview` | `LabCatalog.Import` | DiagnosticImportBatchResponse |
| Manager | GET | `/api/v1/admin/lab-catalog/imports` | `LabCatalog.ImportHistory` | page of DiagnosticImportBatchResponse |
| Manager | GET | `/api/v1/admin/lab-catalog/imports/{batchId}` | `LabCatalog.ImportHistory` | DiagnosticImportBatchResponse |
| Manager | GET | `/api/v1/admin/lab-catalog/imports/{batchId}/changes` | `LabCatalog.ImportHistory` | page of DiagnosticImportRecordResponse |
| Manager | POST | `/api/v1/admin/lab-catalog/imports/{batchId}/apply` | `LabCatalog.Import` | DiagnosticImportBatchResponse; key required |
| Manager | POST | `/api/v1/admin/lab-catalog/imports/{batchId}/discard` | `LabCatalog.Import` | DiagnosticImportBatchResponse |
| Manager | GET | `/api/v1/admin/lab-catalog-requests` | `LabCatalogRequests.View` | page of MedicalCatalogRequestResponse |
| Manager | GET | `/api/v1/admin/lab-catalog-requests/{requestId}` | `LabCatalogRequests.View` | MedicalCatalogRequestResponse |
| Manager | POST | `/api/v1/admin/lab-catalog-requests/{requestId}/request-more-info` | `LabCatalogRequests.Review` | MedicalCatalogRequestResponse |
| Manager | POST | `/api/v1/admin/lab-catalog-requests/{requestId}/approve` | `LabCatalogRequests.Review` | MedicalCatalogRequestResponse; key required |
| Manager | POST | `/api/v1/admin/lab-catalog-requests/{requestId}/reject` | `LabCatalogRequests.Review` | MedicalCatalogRequestResponse |
| Doctor | GET | `/api/v1/doctors/me/lab-catalog` | `LabCatalog.SearchActive` | page of MedicalCatalogResponse |
| Doctor | GET | `/api/v1/doctors/me/lab-catalog-requests` | `LabCatalogRequests.ViewOwn` | page of MedicalCatalogRequestResponse |
| Doctor | GET | `/api/v1/doctors/me/lab-catalog-requests/{requestId}` | `LabCatalogRequests.ViewOwn` | MedicalCatalogRequestResponse |
| Doctor | POST | `/api/v1/doctors/me/lab-catalog-requests` | `LabCatalogRequests.CreateOwn` | MedicalCatalogRequestResponse |
| Doctor | PUT | `/api/v1/doctors/me/lab-catalog-requests/{requestId}` | `LabCatalogRequests.UpdateOwn` | MedicalCatalogRequestResponse |
| Doctor | GET | `/api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/lab-request` | `LabRequests.ViewOwn` | DiagnosticRequestStateResponse |
| Doctor | POST | `/api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/lab-request/items` | `LabRequests.ManageOwnDraft` | DiagnosticRequestStateResponse; key required |
| Doctor | PUT | `/api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/lab-request/items/{itemId}` | `LabRequests.ManageOwnDraft` | DiagnosticRequestStateResponse |
| Doctor | DELETE | `/api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/lab-request/items/{itemId}` | `LabRequests.ManageOwnDraft` | DiagnosticRequestStateResponse |
| Doctor | POST | `/api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/lab-requests/post-visit` | `LabRequests.CreatePostVisitOwn` | DiagnosticRequestStateResponse; key required |
| Doctor | GET | `/api/v1/doctors/me/lab-requests` | `LabRequests.ViewOwn` | page of DiagnosticRequestSummary |
| Doctor | GET | `/api/v1/doctors/me/lab-requests/{requestId}` | `LabRequests.ViewOwn` | DiagnosticRequestStateResponse |
| Doctor | GET | `/api/v1/doctors/me/lab-requests/{requestId}/history` | `LabRequests.ViewOwn` | DiagnosticHistoryResponse[] |
| Doctor | POST | `/api/v1/doctors/me/lab-requests/{requestId}/items/{itemId}/cancel` | `LabRequests.CancelOwn` | DiagnosticRequestStateResponse |
| Doctor | POST | `/api/v1/doctors/me/lab-requests/{requestId}/cancel-remaining` | `LabRequests.CancelOwn` | DiagnosticRequestStateResponse |
| Doctor | POST | `/api/v1/doctors/me/lab-requests/{requestId}/results` | `LabResults.UploadOwn` | DiagnosticResultMutationResponse; key required |
| Doctor | GET | `/api/v1/doctors/me/lab-results` | `LabResults.ViewOwn` | page of DiagnosticResultResponse |
| Doctor | GET | `/api/v1/doctors/me/lab-results/{resultId}` | `LabResults.ViewOwn` | DiagnosticResultResponse |
| Doctor | GET | `/api/v1/doctors/me/lab-results/{resultId}/versions` | `LabResults.ViewOwn` | DiagnosticVersionResponse[] |
| Doctor | GET | `/api/v1/doctors/me/lab-results/{resultId}/versions/{versionNumber}` | `LabResults.ViewOwn` | DiagnosticVersionResponse |
| Doctor | POST | `/api/v1/doctors/me/lab-results/{resultId}/corrections` | `LabResults.CorrectOwn` | DiagnosticResultMutationResponse; key required |
| Doctor | POST | `/api/v1/doctors/me/lab-results/{resultId}/void` | `LabResults.VoidOwn` | DiagnosticResultMutationResponse; key required |
| Doctor | GET | `/api/v1/doctors/me/lab-result-submissions` | `LabResultSubmissions.ViewOwn` | page of DiagnosticSubmissionSummary |
| Doctor | GET | `/api/v1/doctors/me/lab-result-submissions/{submissionId}` | `LabResultSubmissions.ViewOwn` | DiagnosticSubmissionResponse |
| Doctor | POST | `/api/v1/doctors/me/lab-result-submissions/{submissionId}/accept` | `LabResultSubmissions.ReviewOwn` | DiagnosticResultMutationResponse; key required |
| Doctor | POST | `/api/v1/doctors/me/lab-result-submissions/{submissionId}/reject` | `LabResultSubmissions.ReviewOwn` | DiagnosticResultMutationResponse |
| Doctor | GET | `/api/v1/doctors/me/lab-results/{resultId}/versions/{versionNumber}/attachments/{attachmentId}/content` | `LabResults.ViewOwn` | private file stream |
| Doctor | GET | `/api/v1/doctors/me/lab-result-submissions/{submissionId}/attachments/{attachmentId}/content` | `LabResultSubmissions.ViewOwn` | private file stream |
| Patient | GET | `/api/v1/lab-requests/mine` | `LabRequests.ViewOwnIssued` | page of DiagnosticRequestSummary |
| Patient | GET | `/api/v1/lab-requests/mine/{requestId}` | `LabRequests.ViewOwnIssued` | DiagnosticRequestStateResponse |
| Patient | GET | `/api/v1/lab-results/mine` | `LabResults.ViewOwnCurrent` | page of DiagnosticResultResponse |
| Patient | GET | `/api/v1/lab-results/mine/{resultId}` | `LabResults.ViewOwnCurrent` | DiagnosticResultResponse |
| Patient | POST | `/api/v1/lab-requests/mine/{requestId}/submissions` | `LabResultSubmissions.CreateOwn` | DiagnosticResultMutationResponse; key required |
| Patient | GET | `/api/v1/lab-requests/mine/{requestId}/submissions` | `LabResultSubmissions.ViewOwn` | page of DiagnosticSubmissionSummary |
| Patient | GET | `/api/v1/lab-result-submissions/mine/{submissionId}` | `LabResultSubmissions.ViewOwn` | DiagnosticSubmissionResponse |
| Patient | POST | `/api/v1/lab-result-submissions/mine/{submissionId}/withdraw` | `LabResultSubmissions.WithdrawOwn` | DiagnosticResultMutationResponse |
| Patient | GET | `/api/v1/lab-result-submissions/mine/{submissionId}/attachments/{attachmentId}/content` | `LabResultSubmissions.ViewOwn` | private file stream |
| Patient | GET | `/api/v1/lab-results/mine/{resultId}/attachments/{attachmentId}/content` | `LabResults.ViewOwnCurrent` | private file stream |

### Radiology (56)

| Actor | Method | Route | Permission | Response / key |
| --- | --- | --- | --- | --- |
| Manager | GET | `/api/v1/admin/radiology-catalog` | `RadiologyCatalog.View` | page of MedicalCatalogResponse |
| Manager | GET | `/api/v1/admin/radiology-catalog/{id}` | `RadiologyCatalog.View` | MedicalCatalogResponse |
| Manager | POST | `/api/v1/admin/radiology-catalog` | `RadiologyCatalog.Create` | MedicalCatalogResponse |
| Manager | PUT | `/api/v1/admin/radiology-catalog/{id}` | `RadiologyCatalog.Update` | MedicalCatalogResponse |
| Manager | POST | `/api/v1/admin/radiology-catalog/{id}/activate` | `RadiologyCatalog.Activate` | MedicalCatalogResponse |
| Manager | POST | `/api/v1/admin/radiology-catalog/{id}/deactivate` | `RadiologyCatalog.Deactivate` | MedicalCatalogResponse |
| Manager | POST | `/api/v1/admin/radiology-catalog/{id}/merge` | `RadiologyCatalog.Merge` | MedicalCatalogResponse; key required |
| Manager | POST | `/api/v1/admin/radiology-catalog/imports/preview` | `RadiologyCatalog.Import` | DiagnosticImportBatchResponse |
| Manager | GET | `/api/v1/admin/radiology-catalog/imports` | `RadiologyCatalog.ImportHistory` | page of DiagnosticImportBatchResponse |
| Manager | GET | `/api/v1/admin/radiology-catalog/imports/{batchId}` | `RadiologyCatalog.ImportHistory` | DiagnosticImportBatchResponse |
| Manager | GET | `/api/v1/admin/radiology-catalog/imports/{batchId}/changes` | `RadiologyCatalog.ImportHistory` | page of DiagnosticImportRecordResponse |
| Manager | POST | `/api/v1/admin/radiology-catalog/imports/{batchId}/apply` | `RadiologyCatalog.Import` | DiagnosticImportBatchResponse; key required |
| Manager | POST | `/api/v1/admin/radiology-catalog/imports/{batchId}/discard` | `RadiologyCatalog.Import` | DiagnosticImportBatchResponse |
| Manager | GET | `/api/v1/admin/radiology-catalog-requests` | `RadiologyCatalogRequests.View` | page of MedicalCatalogRequestResponse |
| Manager | GET | `/api/v1/admin/radiology-catalog-requests/{requestId}` | `RadiologyCatalogRequests.View` | MedicalCatalogRequestResponse |
| Manager | POST | `/api/v1/admin/radiology-catalog-requests/{requestId}/request-more-info` | `RadiologyCatalogRequests.Review` | MedicalCatalogRequestResponse |
| Manager | POST | `/api/v1/admin/radiology-catalog-requests/{requestId}/approve` | `RadiologyCatalogRequests.Review` | MedicalCatalogRequestResponse; key required |
| Manager | POST | `/api/v1/admin/radiology-catalog-requests/{requestId}/reject` | `RadiologyCatalogRequests.Review` | MedicalCatalogRequestResponse |
| Doctor | GET | `/api/v1/doctors/me/radiology-catalog` | `RadiologyCatalog.SearchActive` | page of MedicalCatalogResponse |
| Doctor | GET | `/api/v1/doctors/me/radiology-catalog-requests` | `RadiologyCatalogRequests.ViewOwn` | page of MedicalCatalogRequestResponse |
| Doctor | GET | `/api/v1/doctors/me/radiology-catalog-requests/{requestId}` | `RadiologyCatalogRequests.ViewOwn` | MedicalCatalogRequestResponse |
| Doctor | POST | `/api/v1/doctors/me/radiology-catalog-requests` | `RadiologyCatalogRequests.CreateOwn` | MedicalCatalogRequestResponse |
| Doctor | PUT | `/api/v1/doctors/me/radiology-catalog-requests/{requestId}` | `RadiologyCatalogRequests.UpdateOwn` | MedicalCatalogRequestResponse |
| Doctor | GET | `/api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/radiology-request` | `RadiologyRequests.ViewOwn` | DiagnosticRequestStateResponse |
| Doctor | POST | `/api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/radiology-request/items` | `RadiologyRequests.ManageOwnDraft` | DiagnosticRequestStateResponse; key required |
| Doctor | PUT | `/api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/radiology-request/items/{itemId}` | `RadiologyRequests.ManageOwnDraft` | DiagnosticRequestStateResponse |
| Doctor | DELETE | `/api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/radiology-request/items/{itemId}` | `RadiologyRequests.ManageOwnDraft` | DiagnosticRequestStateResponse |
| Doctor | POST | `/api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/radiology-requests/post-visit` | `RadiologyRequests.CreatePostVisitOwn` | DiagnosticRequestStateResponse; key required |
| Doctor | GET | `/api/v1/doctors/me/radiology-requests` | `RadiologyRequests.ViewOwn` | page of DiagnosticRequestSummary |
| Doctor | GET | `/api/v1/doctors/me/radiology-requests/{requestId}` | `RadiologyRequests.ViewOwn` | DiagnosticRequestStateResponse |
| Doctor | GET | `/api/v1/doctors/me/radiology-requests/{requestId}/history` | `RadiologyRequests.ViewOwn` | DiagnosticHistoryResponse[] |
| Doctor | POST | `/api/v1/doctors/me/radiology-requests/{requestId}/items/{itemId}/cancel` | `RadiologyRequests.CancelOwn` | DiagnosticRequestStateResponse |
| Doctor | POST | `/api/v1/doctors/me/radiology-requests/{requestId}/cancel-remaining` | `RadiologyRequests.CancelOwn` | DiagnosticRequestStateResponse |
| Doctor | POST | `/api/v1/doctors/me/radiology-requests/{requestId}/results` | `RadiologyResults.UploadOwn` | DiagnosticResultMutationResponse; key required |
| Doctor | GET | `/api/v1/doctors/me/radiology-results` | `RadiologyResults.ViewOwn` | page of DiagnosticResultResponse |
| Doctor | GET | `/api/v1/doctors/me/radiology-results/{resultId}` | `RadiologyResults.ViewOwn` | DiagnosticResultResponse |
| Doctor | GET | `/api/v1/doctors/me/radiology-results/{resultId}/versions` | `RadiologyResults.ViewOwn` | DiagnosticVersionResponse[] |
| Doctor | GET | `/api/v1/doctors/me/radiology-results/{resultId}/versions/{versionNumber}` | `RadiologyResults.ViewOwn` | DiagnosticVersionResponse |
| Doctor | POST | `/api/v1/doctors/me/radiology-results/{resultId}/corrections` | `RadiologyResults.CorrectOwn` | DiagnosticResultMutationResponse; key required |
| Doctor | POST | `/api/v1/doctors/me/radiology-results/{resultId}/void` | `RadiologyResults.VoidOwn` | DiagnosticResultMutationResponse; key required |
| Doctor | GET | `/api/v1/doctors/me/radiology-result-submissions` | `RadiologyResultSubmissions.ViewOwn` | page of DiagnosticSubmissionSummary |
| Doctor | GET | `/api/v1/doctors/me/radiology-result-submissions/{submissionId}` | `RadiologyResultSubmissions.ViewOwn` | DiagnosticSubmissionResponse |
| Doctor | POST | `/api/v1/doctors/me/radiology-result-submissions/{submissionId}/accept` | `RadiologyResultSubmissions.ReviewOwn` | DiagnosticResultMutationResponse; key required |
| Doctor | POST | `/api/v1/doctors/me/radiology-result-submissions/{submissionId}/reject` | `RadiologyResultSubmissions.ReviewOwn` | DiagnosticResultMutationResponse |
| Doctor | GET | `/api/v1/doctors/me/radiology-results/{resultId}/versions/{versionNumber}/attachments/{attachmentId}/content` | `RadiologyResults.ViewOwn` | private file stream |
| Doctor | GET | `/api/v1/doctors/me/radiology-result-submissions/{submissionId}/attachments/{attachmentId}/content` | `RadiologyResultSubmissions.ViewOwn` | private file stream |
| Patient | GET | `/api/v1/radiology-requests/mine` | `RadiologyRequests.ViewOwnIssued` | page of DiagnosticRequestSummary |
| Patient | GET | `/api/v1/radiology-requests/mine/{requestId}` | `RadiologyRequests.ViewOwnIssued` | DiagnosticRequestStateResponse |
| Patient | GET | `/api/v1/radiology-results/mine` | `RadiologyResults.ViewOwnCurrent` | page of DiagnosticResultResponse |
| Patient | GET | `/api/v1/radiology-results/mine/{resultId}` | `RadiologyResults.ViewOwnCurrent` | DiagnosticResultResponse |
| Patient | POST | `/api/v1/radiology-requests/mine/{requestId}/submissions` | `RadiologyResultSubmissions.CreateOwn` | DiagnosticResultMutationResponse; key required |
| Patient | GET | `/api/v1/radiology-requests/mine/{requestId}/submissions` | `RadiologyResultSubmissions.ViewOwn` | page of DiagnosticSubmissionSummary |
| Patient | GET | `/api/v1/radiology-result-submissions/mine/{submissionId}` | `RadiologyResultSubmissions.ViewOwn` | DiagnosticSubmissionResponse |
| Patient | POST | `/api/v1/radiology-result-submissions/mine/{submissionId}/withdraw` | `RadiologyResultSubmissions.WithdrawOwn` | DiagnosticResultMutationResponse |
| Patient | GET | `/api/v1/radiology-result-submissions/mine/{submissionId}/attachments/{attachmentId}/content` | `RadiologyResultSubmissions.ViewOwn` | private file stream |
| Patient | GET | `/api/v1/radiology-results/mine/{resultId}/attachments/{attachmentId}/content` | `RadiologyResults.ViewOwnCurrent` | private file stream |

## Localized diagnostic code inventory

```text
LabCatalog.NotFound
LabCatalog.NotActive
LabCatalog.AlreadyInactive
LabCatalog.Merged
LabCatalog.DuplicateLoincCode
LabCatalog.PossibleDuplicate
LabCatalog.SourceFieldsReadOnly
LabCatalog.InvalidMergeTarget
LabCatalog.MergeCycle
LabCatalog.ConcurrencyConflict
LabCatalog.InvalidState
LabCatalog.InvalidData
LabCatalog.ReasonRequired
LabCatalogRequest.NotFound
LabCatalogRequest.InvalidState
LabCatalogRequest.NotOwner
LabCatalogRequest.MoreInfoReasonRequired
LabCatalogRequest.RejectionReasonRequired
LabCatalogRequest.InvalidDuplicateTarget
LabCatalogRequest.ConcurrencyConflict
LabCatalogRequest.InvalidData
LabRequest.NotFound
LabRequest.EncounterNotOwned
LabRequest.InvalidEncounterState
LabRequest.PostVisitReasonRequired
LabRequest.Empty
LabRequest.DuplicateTest
LabRequest.SourceRequired
LabRequest.MultipleSources
LabRequest.ItemNotFound
LabRequest.ItemNotRequested
LabRequest.CompletedItemCannotBeCancelled
LabRequest.CancellationReasonRequired
LabRequest.ConcurrencyConflict
LabRequest.InvalidState
LabRequest.InvalidData
LabResultSubmission.NotFound
LabResultSubmission.NotOwner
LabResultSubmission.NotPending
LabResultSubmission.NoRequestedItems
LabResultSubmission.InvalidCoverage
LabResultSubmission.RejectionReasonRequired
LabResultSubmission.NoLongerApplicable
LabResultSubmission.ConcurrencyConflict
LabResultSubmission.InvalidData
LabResult.NotFound
LabResult.AttachmentRequired
LabResult.CoverageRequired
LabResult.InvalidCoverage
LabResult.ItemNotRequested
LabResult.NoCurrentVersion
LabResult.InvalidCorrectionState
LabResult.InvalidVoidState
LabResult.VoidReasonRequired
LabResult.CorrectionReasonRequired
LabResult.ConcurrencyConflict
LabResult.InvalidData
LabResult.AttachmentNotFound
RadiologyCatalog.NotFound
RadiologyCatalog.NotActive
RadiologyCatalog.AlreadyInactive
RadiologyCatalog.Merged
RadiologyCatalog.DuplicateLoincCode
RadiologyCatalog.PossibleDuplicate
RadiologyCatalog.SourceFieldsReadOnly
RadiologyCatalog.InvalidMergeTarget
RadiologyCatalog.MergeCycle
RadiologyCatalog.ConcurrencyConflict
RadiologyCatalog.InvalidState
RadiologyCatalog.InvalidData
RadiologyCatalog.ReasonRequired
RadiologyCatalogRequest.NotFound
RadiologyCatalogRequest.InvalidState
RadiologyCatalogRequest.NotOwner
RadiologyCatalogRequest.MoreInfoReasonRequired
RadiologyCatalogRequest.RejectionReasonRequired
RadiologyCatalogRequest.InvalidDuplicateTarget
RadiologyCatalogRequest.ConcurrencyConflict
RadiologyCatalogRequest.InvalidData
RadiologyRequest.NotFound
RadiologyRequest.EncounterNotOwned
RadiologyRequest.InvalidEncounterState
RadiologyRequest.PostVisitReasonRequired
RadiologyRequest.Empty
RadiologyRequest.DuplicateTest
RadiologyRequest.SourceRequired
RadiologyRequest.MultipleSources
RadiologyRequest.ItemNotFound
RadiologyRequest.ItemNotRequested
RadiologyRequest.CompletedItemCannotBeCancelled
RadiologyRequest.CancellationReasonRequired
RadiologyRequest.ConcurrencyConflict
RadiologyRequest.InvalidState
RadiologyRequest.InvalidData
RadiologyResultSubmission.NotFound
RadiologyResultSubmission.NotOwner
RadiologyResultSubmission.NotPending
RadiologyResultSubmission.NoRequestedItems
RadiologyResultSubmission.InvalidCoverage
RadiologyResultSubmission.RejectionReasonRequired
RadiologyResultSubmission.NoLongerApplicable
RadiologyResultSubmission.ConcurrencyConflict
RadiologyResultSubmission.InvalidData
RadiologyResult.NotFound
RadiologyResult.AttachmentRequired
RadiologyResult.CoverageRequired
RadiologyResult.InvalidCoverage
RadiologyResult.ItemNotRequested
RadiologyResult.NoCurrentVersion
RadiologyResult.InvalidCorrectionState
RadiologyResult.InvalidVoidState
RadiologyResult.VoidReasonRequired
RadiologyResult.CorrectionReasonRequired
RadiologyResult.ConcurrencyConflict
RadiologyResult.InvalidData
RadiologyResult.AttachmentNotFound
DiagnosticCatalogImport.PackageTooLarge
DiagnosticCatalogImport.InvalidArchive
DiagnosticCatalogImport.UnsafeArchive
DiagnosticCatalogImport.RequiredFileMissing
DiagnosticCatalogImport.InvalidHeaders
DiagnosticCatalogImport.UnsupportedEncoding
DiagnosticCatalogImport.DuplicateSourceIdentity
DiagnosticCatalogImport.UnsupportedSourceVersion
DiagnosticCatalogImport.BatchNotFound
DiagnosticCatalogImport.BatchNotStaged
DiagnosticCatalogImport.AlreadyApplied
DiagnosticCatalogImport.AlreadyDiscarded
DiagnosticCatalogImport.ConcurrencyConflict
DiagnosticCatalogImport.PossibleConflict
Diagnostics.AccessDenied
```

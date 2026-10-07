# Phase 15 Source-of-Truth addendum — 2026-10-07

Status of every rule below: **CONFIRMED-V1**. Implementation status is tracked separately in PHASE-15-CLOSURE-REPORT.md; approval of a rule is not evidence of implementation.

Branch: `codex/phase15-diagnostic-orders-results`, based on clean `main`.

This extends Phase 13 and Phase 14 without rewriting their approved decisions. The former diagnostic intake blockers are resolved: Patients may upload Lab and Radiology reports only as their own PendingReview submissions. The same requesting, approved and active Doctor alone may accept a submission or upload a reviewed report directly. Only that clinical action creates an Official Result. Patient uploads never complete order items or create official results. Reception, catalog managers and platform administrators receive no clinical override.

The complete approved task is retained below so every requirement, delivery rule and exclusion has a durable source. Each numbered section is CONFIRMED-V1.

---
CODEX TASK — PHASE 15: DIAGNOSTIC ORDERS & RESULTS

Repository:

LoaiSaberC4r/Wasla

Target:

.NET 10
C# 14
SQL Server
Clean Architecture
CQRS / MediatR
FluentValidation
Result Pattern
Specification Pattern
Repository / UnitOfWork
ITransactionalCommand
Optimistic Concurrency
ProblemDetails
Localization ar/en
Audit / Immutable Clinical History
Private Media
Idempotency

---

0. IMPORTANT WORKING RULE — CONFIRMED-V1

Do NOT jump directly to implementation.

Before changing code:

1. Inspect current repository "main". — CONFIRMED-V1
2. Read: — CONFIRMED-V1
   - "README.md"
   - current Phase 13 Source-of-Truth addendum
   - current Phase 14 Source-of-Truth addendum
   - "PHASE-14-API-CHANGES.md"
   - "PHASE-14-CLOSURE-REPORT.md"
3. Inspect actual Phase 14 implementations for: — CONFIRMED-V1
   - "DrugCatalogManager"
   - Drug Catalog
   - offline imports
   - Drug Catalog Requests
   - Prescription draft/version/correction/void
   - Idempotency
   - permissions/seeding
   - Root-only governance
   - Complete Visit integration
4. Inspect the existing BuildingBlock capabilities before creating replacements: — CONFIRMED-V1
   - "AggregateRoot"
   - "Entity"
   - "Result/Error"
   - "Specification"
   - "IReadRepository"
   - "IWriteRepository"
   - "IUnitOfWork"
   - "ITransactionalCommand"
   - Transaction behavior
   - "ICurrentUser"
   - auditing interceptors
   - media abstractions
   - media validation
   - ProblemDetails
   - localization
5. QControl may be consulted only for: — CONFIRMED-V1
   - permission pattern
   - reasoned state transitions
   - immutable history concepts
   - scoped operations

Do NOT copy QControl domain code for Lab/Radiology.

If implementation reality differs from this task, preserve the approved business rules and adapt technically to the existing Wasla conventions.

Do not invent new business rules.

---

1. PHASE STATUS — CONFIRMED-V1

Current repository:

Phase 14 — Drug Catalog & Prescriptions — DONE

Implement:

Phase 15 — Diagnostic Orders & Results

Scope:

Lab
Radiology
MedicalCatalogManager
Catalog Import
Catalog Review Requests
Diagnostic Requests
Patient Report Submissions
Doctor Result Review
Official Results
Result Correction / Void
Private Medical Media
Patient Views

After successful completion update README to:

Current Stage:
Phase 15 — Diagnostic Orders & Results — DONE

Only after build/tests/migration/contracts/closure are complete.

---

2. BUSINESS PURPOSE — CONFIRMED-V1

Phase 15 completes the next part of the outpatient clinical journey:

Medical Encounter
      ↓
Diagnosis / Prescription
      ↓
Lab / Radiology Request
      ↓
Patient performs investigation externally
      ↓
External Report
      ↓
Requesting Doctor reviews
      ↓
Official Wasla Result
      ↓
Patient clinical history

V1 deliberately does NOT require laboratories or radiology centers to be actors inside Wasla.

Patients may perform investigations at any external provider.

Wasla's job in V1 is:

Order
→ collect report
→ Doctor review
→ trusted clinical record

---

3. SOURCE-OF-TRUTH ADDENDUM FIRST — CONFIRMED-V1

Before implementation create:

SOURCE-OF-TRUTH-PHASE-15-ADDENDUM.md

Record all rules in this task as:

CONFIRMED-V1

The addendum must explicitly resolve the old Phase 15 blockers:

Who may upload Lab results?
Who may upload Radiology results?
Who clinically accepts them?
How Patient uploads differ from Official Results?

Do not rewrite previous approved Phase 13/14 decisions.

Phase 15 extends them.

---

4. KEY CLINICAL AUTHORIZATION RULE — CONFIRMED-V1

Authorization MUST always be:

Authentication
+
Permission
+
Actor State
+
Resource Scope
+
Business Relationship
+
Resource State

Never Role-only.

Never Permission-only.

---

5. MEDICAL CATALOG MANAGER — CONFIRMED-V1

Introduce:

MedicalCatalogManager

This is a platform account type dedicated to non-drug medical reference catalogs.

Initially manages:

LabTestCatalog
RadiologyProcedureCatalog

"DrugCatalogManager" remains separate and unchanged.

---

6. MEDICAL CATALOG MANAGER GOVERNANCE — CONFIRMED-V1

Only the original/seeded Root SuperAdmin may:

View MedicalCatalogManagers
Create
Update
Activate
Deactivate
Reactivate

Ordinary SuperAdmins MUST NOT manage these accounts.

MedicalCatalogManager MUST NOT manage itself or another manager.

Root governance does NOT grant Root:

Lab clinical access
Radiology clinical access
Catalog review access
Patient result access

Follow the existing DrugCatalogManager Root-governance architecture.

New accounts use the existing first-login/change-password lifecycle.

---

7. MEDICAL CATALOG MANAGER PERMISSIONS — CONFIRMED-V1

Seed:

LabCatalog.View
LabCatalog.Create
LabCatalog.Update
LabCatalog.Activate
LabCatalog.Deactivate
LabCatalog.Merge
LabCatalog.Import
LabCatalog.ImportHistory

LabCatalogRequests.View
LabCatalogRequests.Review

RadiologyCatalog.View
RadiologyCatalog.Create
RadiologyCatalog.Update
RadiologyCatalog.Activate
RadiologyCatalog.Deactivate
RadiologyCatalog.Merge
RadiologyCatalog.Import
RadiologyCatalog.ImportHistory

RadiologyCatalogRequests.View
RadiologyCatalogRequests.Review

MedicalCatalogManager MUST NOT receive:

Patient clinical permissions
MedicalEncounter permissions
Prescription permissions
LabRequest permissions
LabResult permissions
RadiologyRequest permissions
RadiologyResult permissions
Payment permissions
Queue permissions

---

8. ROOT PERMISSIONS — CONFIRMED-V1

Seed:

MedicalCatalogManagers.ViewAll
MedicalCatalogManagers.ViewDetails
MedicalCatalogManagers.Create
MedicalCatalogManagers.Update
MedicalCatalogManagers.Activate
MedicalCatalogManagers.Deactivate

In addition to permission checks, handlers must validate the existing original Root identity invariant.

---

9. LAB CATALOG — CONFIRMED-V1

Create central:

LabTestCatalog

Source types:

Loinc
Wasla

Lifecycle:

Active
Inactive
Merged

No hard delete.

---

10. LAB LOINC SOURCE — CONFIRMED-V1

Use official LOINC release package.

The development/reference package used for this design is:

Loinc_2.83.zip

Do NOT seed tens of thousands of LOINC records through EF "HasData".

Do NOT place the full LOINC dataset inside migrations.

Do NOT create runtime internet dependency on LOINC.

Implement controlled offline ZIP import.

---

11. LAB IMPORT FILTER — CONFIRMED-V1

From:

LoincTable/Loinc.csv

Lab concepts are eligible for Wasla source import when:

CLASSTYPE = 1
AND
ORDER_OBS IN ("Order", "Both")

Doctor selectable:

STATUS = ACTIVE

Keep source information/history for non-active concepts where imported.

Statuses such as:

TRIAL
DISCOURAGED
DEPRECATED

must NOT be selectable for new Doctor orders.

Do not hard-delete historical catalog rows when LOINC changes status.

---

12. LAB SOURCE FIELDS — CONFIRMED-V1

Retain relevant source-owned fields including where available:

LOINC_NUM
COMPONENT
PROPERTY
TIME_ASPCT
SYSTEM
SCALE_TYP
METHOD_TYP
CLASS
STATUS
CLASSTYPE
SHORTNAME
LONG_COMMON_NAME
DisplayName
ORDER_OBS
EXAMPLE_UNITS
EXAMPLE_UCUM_UNITS
COMMON_TEST_RANK
COMMON_ORDER_RANK
VersionFirstReleased
VersionLastChanged
EXTERNAL_COPYRIGHT_NOTICE
EXTERNAL_COPYRIGHT_LINK

Use column names, not fixed CSV positions.

---

13. COMMON LAB ORDERS — CONFIRMED-V1

Read:

LoincUniversalLabOrdersValueSet.csv

Do NOT limit the Wasla Lab catalog to this file.

Use it only as:

IsCommonOrder
and/or
search ranking signal

Common orders should rank higher in Doctor search.

---

14. ARABIC LOINC — CONFIRMED-V1

Read:

arJO32LinguisticVariant.csv

Arabic coverage is partial.

Rules:

Official Arabic available
→ import OfficialNameAr

Official Arabic missing
→ OfficialNameAr = null

Never machine-translate missing canonical names and store them as official terminology.

Wasla may maintain separate:

DisplayNameAr
DisplayNameEn
AliasesAr
AliasesEn

Those are local presentation/search fields.

---

15. LAB CATALOG OWNERSHIP — CONFIRMED-V1

LOINC-backed records:

Source-owned clinical fields are read-only to manual edit.

MedicalCatalogManager may edit local Wasla presentation fields such as:

DisplayNameAr
DisplayNameEn
AliasesAr
AliasesEn
local/internal presentation metadata

Import updates source-owned fields.

Import MUST NOT overwrite local presentation/aliases.

Wasla-created records may have:

Source = Wasla
LoincCode = null

---

16. LAB CATALOG MERGE — CONFIRMED-V1

Allow:

Source catalog record
→ Merged
→ Active canonical target

Require:

reason
rowVersion
Idempotency-Key

Rules:

source != target
target must be Active
source cannot already be Merged
no cycles
resolve/prevent long merge chains
Merged is terminal

Historical LabRequest snapshots MUST NOT be rewritten.

Searching an old alias may resolve/suggest the canonical target.

---

17. LAB CATALOG IMPORT MODEL — CONFIRMED-V1

Create equivalent Phase-14-style import model:

LabCatalogImportBatch
LabCatalogImportRecord

Batch stores at minimum:

Id
SourceVersion
OriginalFileName
FileSha256
UploadedByUserId
UploadedAtUtc
Status
counts
AppliedAtUtc?
AppliedByUserId?
RowVersion

Statuses:

Staged
Applied
Discarded

Record dispositions:

New
Unchanged
Changed
ArabicAdded
ArabicChanged
ExternalStatusChanged
PossibleConflict
Skipped

---

18. LAB IMPORT FLOW — CONFIRMED-V1

ZIP Upload
↓
Validate
↓
Stage
↓
Preview
↓
Review
↓
Apply

Preview must NOT modify runtime catalog.

Apply must:

be transactional
be idempotent
be concurrency safe
be serialized against another active apply

Applied batches are immutable history.

No blind rollback.

If a future release removes a code:

DO NOT auto-delete
DO NOT blindly deactivate

Record source presence/last-seen information if useful and require reviewed behavior.

---

19. LAB IMPORT PACKAGE SECURITY — CONFIRMED-V1

Create dedicated import configuration.

Recommended:

DiagnosticCatalogImport.MaxPackageBytes = 200 MiB

Protect against:

ZIP path traversal
ZIP bombs / excessive expansion
excessive entry count
missing required CSV
invalid headers
unsupported encoding
duplicate source identities
unexpected file structure

Calculate SHA-256.

Do not treat LOINC ZIP as Patient media.

Do not use the normal 10 MiB medical attachment limit for the LOINC package.

---

20. LAB CATALOG REQUEST — CONFIRMED-V1

Create:

LabCatalogRequest
LabCatalogRequestHistory

Only Approved/Active Doctor may create a missing-test request.

Minimum input:

TestName required
Specimen optional
CatalogClarificationNote optional

It MUST NOT contain:

PatientId
MedicalEncounterId
PracticeId

This is reference-data review, not clinical data.

---

21. LAB CATALOG REQUEST LIFECYCLE — CONFIRMED-V1

Pending
→ NeedsMoreInfo
→ Pending

Pending → Approved
Pending → Rejected

Approved / Rejected terminal.

Doctor may update own:

Pending
NeedsMoreInfo

Editing "NeedsMoreInfo" means resubmit to:

Pending

MedicalCatalogManager may:

RequestMoreInfo
Approve
Reject

MoreInfo and Reject require reason.

---

22. DUPLICATE LAB REQUEST — CONFIRMED-V1

If submitted test already exists:

Rejected
ReasonType = Duplicate
CanonicalLabTestCatalogId = existing catalog item

Exact internal representation may follow existing DrugCatalogRequest conventions.

---

23. LAB CATALOG APPROVAL — CONFIRMED-V1

Approval may:

link/create LOINC-backed canonical record

or:

create Wasla-only canonical record

Approval + canonical catalog mutation/link + history must commit atomically.

Manager never receives Patient/Encounter context.

---

24. DOCTOR MAY USE MISSING TEST IMMEDIATELY — CONFIRMED-V1

Doctor MUST NOT wait for catalog review.

When adding a missing test during a clinical order, the same command must atomically:

Create LabCatalogRequest
+
Create DoctorSubmitted LabRequestItem

Do not make frontend perform two API calls.

Later catalog approval/rejection MUST NOT rewrite historical LabRequestItem snapshots.

---

25. LAB REQUEST AGGREGATE — CONFIRMED-V1

Create:

LabRequest
LabRequestItem
LabRequestHistory

"LabRequest" is a separate clinical Aggregate Root.

It links to exactly one:

MedicalEncounter
Patient
Doctor
DoctorPractice

PatientId/DoctorId/PracticeId must be derived from the trusted MedicalEncounter.

Do NOT trust client-provided ownership IDs.

---

26. LAB REQUEST ORIGIN — CONFIRMED-V1

DuringEncounter
PostVisit

---

27. DURING-ENCOUNTER LAB DRAFT — CONFIRMED-V1

For an:

InProgress MedicalEncounter

allow at most:

0..1 Draft LabRequest

The Doctor owning the Encounter may:

Add item
Update item DoctorInstructions
Remove item

If the final item is removed, remove the empty Draft root.

This is the only normal destructive removal allowed because the order has not yet been issued.

---

28. COMPLETE VISIT INTEGRATION — CONFIRMED-V1

Extend the existing authoritative Complete Visit flow.

If a LabRequest Draft exists:

Draft → Requested

in the SAME transaction as:

Ticket completion
MedicalEncounter completion
Prescription finalization if applicable
Follow-up logic
Radiology Draft publication if applicable

Complete Visit remains atomic.

A visit may complete without Lab/Radiology.

Extend the current completion contract with:

labRequestRowVersion?
radiologyRequestRowVersion?

If the corresponding Draft exists, missing/stale token must conflict.

---

29. POST-VISIT LAB REQUEST — CONFIRMED-V1

The same Doctor owning the completed MedicalEncounter may create a new LabRequest after completion.

This is:

PostVisit Diagnostic Order

It MUST NOT:

reopen Encounter
modify Encounter
create EncounterAmendment

Require:

PostVisitReason

PostVisitReason is internal clinical/audit information.

PostVisit request is created directly as:

Requested

No long-lived PostVisit Draft.

---

30. LAB REQUEST ITEMS — CONFIRMED-V1

Each item has exactly one source:

Catalog
XOR
DoctorSubmitted

Catalog item:

LabTestCatalogId

DoctorSubmitted item:

LabCatalogRequestId

Store historical snapshots.

At minimum:

TestNameArSnapshot
TestNameEnSnapshot
LoincCodeSnapshot

Do not silently rewrite snapshots later.

Reject duplicate tests inside the same LabRequest.

---

31. LAB REQUEST INSTRUCTIONS — CONFIRMED-V1

"LabRequest":

PatientInstructions?

patient-visible.

"LabRequestItem":

DoctorInstructions?

patient-visible.

"PostVisitReason":

internal only

Do not add in V1:

Urgent/Routine/STAT workflow
DueDate
Automatic Expiry

---

32. LAB REQUEST STATES — CONFIRMED-V1

"LabRequest":

Draft
Requested
PartiallyCompleted
Completed
Cancelled

"LabRequestItem":

Requested
Completed
Cancelled

No "InProgress" in V1.

Wasla does not observe external laboratory processing.

---

33. REQUEST STATUS CALCULATION — CONFIRMED-V1

Prefer derived/recalculated parent state based on item state.

Rules:

all active items Requested
→ Requested

some Completed + some Requested
→ PartiallyCompleted

all terminal + >=1 Completed
→ Completed

all Cancelled
→ Cancelled

Do not create inconsistent independent state.

---

34. LAB CANCELLATION — CONFIRMED-V1

Only Requesting Doctor may cancel.

Only:

Requested → Cancelled

is allowed.

Completed item cannot be cancelled.

Cancellation requires:

Reason
Actor
Timestamp

Reason is patient-visible.

Whole request cancellation action means:

cancel all remaining Requested items

If some items are already Completed:

Completed stay Completed
remaining Requested → Cancelled
parent → Completed

No issued LabRequest hard delete.

---

35. PATIENT CANNOT CLINICALLY CANCEL ORDER — CONFIRMED-V1

Do not implement:

PatientDeclined

in V1.

Patient choosing not to perform a test does not change the Doctor's clinical order.

---

36. EXTERNAL LAB MODEL — CONFIRMED-V1

V1 does NOT require:

Lab entity
LaboratoryId
Lab account
Lab assignment
direct Lab integration

Patient may use any external laboratory.

Optional provenance:

ExternalLaboratoryName
ExternalReportDate

free-text/date only.

---

37. RESULT TRUST MODEL — CONFIRMED-V1

Very important:

Patient uploaded file
!=
Official clinical result

An Official LabResult exists only after review by the SAME Doctor who created the LabRequest.

Do NOT use generic "Verified" terminology suggesting Wasla certifies the laboratory.

The meaning is:

Reviewed and recorded by the requesting Doctor.

---

38. RESULT INTAKE CHANNEL A — OUTSIDE WASLA — CONFIRMED-V1

Flow:

Patient performs test externally
↓
gives report to Doctor outside Wasla
↓
Requesting Doctor reviews it
↓
Doctor uploads
↓
Official LabResult

---

39. RESULT INTAKE CHANNEL B — PATIENT UPLOAD — CONFIRMED-V1

Create:

PatientLabResultSubmission
PatientLabResultSubmissionAttachment
PatientLabResultSubmissionHistory

Patient uploads report against own issued LabRequest.

The Patient MUST NOT choose covered test items.

Submission lifecycle:

PendingReview
→ Accepted

PendingReview
→ Rejected

PendingReview
→ Withdrawn

PendingReview
→ NoLongerApplicable

Terminal:

Accepted
Rejected
Withdrawn
NoLongerApplicable

---

40. PATIENT SUBMISSION RULES — CONFIRMED-V1

Patient upload MUST NOT:

create LabResult
complete LabRequestItem
change LabRequest state

Requesting Doctor alone reviews it.

Reject requires:

patient-visible reason

Patient may submit a new report after rejection.

Patient may Withdraw only while PendingReview.

No auto-expiration.

---

41. NO-LONGER-APPLICABLE — CONFIRMED-V1

If all relevant/requestable items become Cancelled while a Patient submission is still PendingReview:

PendingReview
→ NoLongerApplicable

Do not mark this as clinical rejection.

---

42. DOCTOR SUSPENSION — CONFIRMED-V1

Suspended Doctor:

cannot access clinical Lab records
cannot upload results
cannot review submissions

Pending submissions remain Pending.

No automatic reassignment.

Reactivation restores the Doctor's original ownership.

Do NOT introduce cross-Doctor review in V1.

---

43. DOCTOR LAB REVIEW INBOX — CONFIRMED-V1

Implement Doctor-scoped inbox.

Default:

PendingReview
ORDER BY SubmittedAtUtc ASC

Filters:

status
practiceId
patientId
fromUtc
toUtc
pageNumber
pageSize

Only submissions whose underlying LabRequest belongs to CurrentDoctor.

---

44. OFFICIAL LAB RESULT — CONFIRMED-V1

Create:

LabResult
LabResultVersion
LabResultCoverage
LabResultAttachment
LabResultHistory

One LabResult represents one result/report set associated with one LabRequest.

One result may cover multiple request items.

Example:

CBC
HbA1c
TSH

One PDF may cover all three.

Do NOT duplicate the PDF per LabRequestItem.

---

45. RESULT VERSIONING — CONFIRMED-V1

Result versions:

Finalized
Superseded
Voided

Initial upload:

Version 1 = Finalized/current

Correction:

old Finalized → Superseded
new version → Finalized/current

No ordinary overwrite.

No hard delete.

---

46. RESULT COVERAGE — CONFIRMED-V1

Doctor explicitly selects covered:

LabRequestItemIds

Backend must verify:

all belong to same LabRequest
all belong to same requesting Doctor
all currently Requested

At least one item required.

---

47. RESULT DOCUMENT-FIRST V1 — CONFIRMED-V1

Do NOT implement structured numeric observation modeling such as:

Value
Unit
ReferenceRange
AbnormalFlag
structured panels

V1 is document-first.

Result contains external report documents/images and provenance.

---

48. RESULT CORRECTION — CONFIRMED-V1

Use one-shot correction in V1.

Do NOT create Prescription-style correction draft.

Correction requires:

Reason
new attachments
new/current coveredItemIds
rowVersion

Operation atomically:

Supersede old current version
Create new finalized version
Update coverage
Recalculate item/request state

Patient continues to see old current result until successful correction commit, then switches to new current result.

---

49. RESULT VOID — CONFIRMED-V1

Requesting Doctor only.

Require:

Reason
RowVersion
Idempotency-Key

If the voided result was the only valid coverage for an item:

Completed → Requested

then recalculate request.

Voiding a diagnostic result does NOT reopen the MedicalEncounter.

Patient must not be able to download a Voided attachment.

---

50. PATIENT RESULT VISIBILITY — CONFIRMED-V1

Patient sees LabRequest once:

Status != Draft

Patient sees Official Result immediately after successful Requesting Doctor upload/acceptance.

No separate:

ReleaseToPatient

action.

Patient sees only:

current valid result

Patient does NOT see:

superseded versions
internal correction reasons
internal void reasons
catalog review metadata
audit internals
PostVisitReason

---

51. PATIENT LAB VIEW — CONFIRMED-V1

Expose:

Doctor
Practice
Request date
Origin
Request status
PatientInstructions
Tests
Per-item status
DoctorInstructions
CancellationReason
Current result
Submission statuses

Draft clinical requests remain hidden.

---

52. PRIVATE LAB MEDIA — CONFIRMED-V1

Supported V1 types:

.pdf
.jpg
.jpeg
.png

No:

gif
mp4
svg
html
scripts
executables

Recommended limits:

10 MiB per attachment
20 attachments per submission/result
100 MiB aggregate per submission/result

Use existing BuildingBlock media validation/storage rather than creating a parallel media framework.

Use extension + content type + file signature validation.

Preserve malware scanning integration path.

---

53. PRIVATE MEDIA METADATA — CONFIRMED-V1

Persist metadata such as:

Id
PrivateMediaKey
OriginalFileName
ContentType
SizeBytes
Sha256
UploadedAtUtc

Do NOT store:

public URL
raw binary file inside clinical SQL tables

Each content-download endpoint must authorize retrieval again.

---

54. PATIENT SUBMISSION ACCEPT — CONFIRMED-V1

Accept MUST be one atomic command:

Submission PendingReview → Accepted
+
Create Official LabResult
+
Retain original uploader provenance
+
reuse/reference accepted private attachment safely
+
Create coverage
+
Complete covered items
+
Recalculate LabRequest

Do not require frontend to call Result creation separately.

Persist provenance:

OriginallyUploadedBy = Patient
OriginallyUploadedAtUtc
ReviewedByDoctorId
ReviewedAtUtc

Doctor direct upload:

OriginallyUploadedBy = Doctor

---

55. RADIOLOGY CATALOG — CONFIRMED-V1

Create:

RadiologyProcedureCatalog

Lifecycle/pattern follows Lab Catalog.

External source:

LOINC / RSNA Radiology Playbook

Use the same official LOINC release ZIP.

---

56. RADIOLOGY IMPORT — CONFIRMED-V1

Read:

LoincRsnaRadiologyPlaybook.csv

Also use:

Loinc.csv

for canonical status/metadata.

Do NOT create one procedure per CSV attribute row.

Group Radiology Playbook data by:

LoincNumber

to produce one Wasla procedure with structured attributes.

---

57. RADIOLOGY ATTRIBUTES — CONFIRMED-V1

Preserve where available:

Modality
Modality Subtype

Region Imaged
Imaging Focus
Laterality

View Type
View Aggregation

Timing

Pharmaceutical / Substance
Route

Guidance Action
Guidance Object
Guidance Approach

Reason for Exam
Maneuver
Subject

Use actual Playbook PartTypeName values rather than assumptions.

---

58. RADIOLOGY ARABIC — CONFIRMED-V1

Do not assume Arabic exists.

If official Arabic terminology is present for a code:

import it

otherwise:

OfficialNameAr = null

MedicalCatalogManager may maintain Wasla:

DisplayNameAr
AliasesAr

Never machine-translate and store as official source data.

---

59. RADIOLOGY CATALOG REQUEST — CONFIRMED-V1

Mirror Lab:

RadiologyCatalogRequest
RadiologyCatalogRequestHistory

Lifecycle:

Pending
NeedsMoreInfo
Approved
Rejected

Doctor can use DoctorSubmitted procedure immediately without waiting for review.

Approval/rejection later never rewrites historical RadiologyRequest snapshot.

---

60. RADIOLOGY REQUEST — CONFIRMED-V1

Create:

RadiologyRequest
RadiologyRequestItem
RadiologyRequestHistory

Same clinical ordering rules as Lab:

one Draft per InProgress Encounter
Complete Visit publishes Draft
PostVisit creates a new Requested order
Requesting Doctor ownership
cancellation
history

States mirror Lab.

Do not create a generic mega aggregate such as:

DiagnosticRequest<T>

Lab and Radiology should remain separate Domain models.

Technical patterns/helpers may be reused.

---

61. RADIOLOGY ITEM SNAPSHOT — CONFIRMED-V1

Keep historical information sufficient to understand what was ordered.

At minimum where applicable:

NameArSnapshot
NameEnSnapshot
LoincCodeSnapshot
ModalitySnapshot
AnatomicLocationSnapshot
LateralitySnapshot

---

62. RADIOLOGY RESULT — CONFIRMED-V1

Create parallel model:

RadiologyResult
RadiologyResultVersion
RadiologyResultCoverage
RadiologyResultAttachment
RadiologyResultHistory

PatientRadiologyResultSubmission
PatientRadiologyResultSubmissionAttachment
PatientRadiologyResultSubmissionHistory

Result workflow mirrors Lab.

---

63. RADIOLOGY MEDIA — CONFIRMED-V1

Attachments must have:

Kind

Enum:

Report
Image

Allow:

PDF
JPG/JPEG
PNG

Do NOT implement in V1:

DICOM
PACS
DICOM viewer
direct Radiology Center integration

---

64. DOCTOR PERMISSIONS — CONFIRMED-V1

Seed Lab:

LabCatalog.SearchActive

LabCatalogRequests.CreateOwn
LabCatalogRequests.ViewOwn
LabCatalogRequests.UpdateOwn

LabRequests.ViewOwn
LabRequests.ManageOwnDraft
LabRequests.CreatePostVisitOwn
LabRequests.CancelOwn

LabResults.ViewOwn
LabResults.UploadOwn
LabResults.CorrectOwn
LabResults.VoidOwn

LabResultSubmissions.ViewOwn
LabResultSubmissions.ReviewOwn

Seed equivalent Radiology permissions:

RadiologyCatalog.SearchActive

RadiologyCatalogRequests.CreateOwn
RadiologyCatalogRequests.ViewOwn
RadiologyCatalogRequests.UpdateOwn

RadiologyRequests.ViewOwn
RadiologyRequests.ManageOwnDraft
RadiologyRequests.CreatePostVisitOwn
RadiologyRequests.CancelOwn

RadiologyResults.ViewOwn
RadiologyResults.UploadOwn
RadiologyResults.CorrectOwn
RadiologyResults.VoidOwn

RadiologyResultSubmissions.ViewOwn
RadiologyResultSubmissions.ReviewOwn

---

65. PATIENT PERMISSIONS — CONFIRMED-V1

Lab:

LabRequests.ViewOwnIssued
LabResults.ViewOwnCurrent

LabResultSubmissions.CreateOwn
LabResultSubmissions.ViewOwn
LabResultSubmissions.WithdrawOwn

Radiology:

RadiologyRequests.ViewOwnIssued
RadiologyResults.ViewOwnCurrent

RadiologyResultSubmissions.CreateOwn
RadiologyResultSubmissions.ViewOwn
RadiologyResultSubmissions.WithdrawOwn

---

66. RECEPTION — CONFIRMED-V1

Reception gets:

NO Lab clinical permissions
NO Radiology clinical permissions

Reception must not see reports/results because it can operate the Patient/Reservation/Ticket workflow.

Operational access does not imply clinical access.

---

67. SUPERADMIN — CONFIRMED-V1

Ordinary SuperAdmin:

NO Lab clinical permissions
NO Radiology clinical permissions
NO MedicalCatalogManager account governance

Root:

MedicalCatalogManager account governance only

Root does NOT automatically receive clinical browsing rights.

---

68. DOCTOR ACTOR STATE — CONFIRMED-V1

Clinical actions require:

ApplicationUser active
Doctor approved
Doctor active
required permission
own resource relationship
valid resource state

Suspended Doctor is denied clinical Lab/Radiology operations.

---

69. REQUIRED ENDPOINTS — MANAGER GOVERNANCE — CONFIRMED-V1

Implement:

GET  /api/v1/admin/medical-catalog-managers
GET  /api/v1/admin/medical-catalog-managers/{id}

POST /api/v1/admin/medical-catalog-managers
PUT  /api/v1/admin/medical-catalog-managers/{id}

POST /api/v1/admin/medical-catalog-managers/{id}/activate
POST /api/v1/admin/medical-catalog-managers/{id}/deactivate

Reuse existing first-password lifecycle.

---

70. REQUIRED ENDPOINTS — DOCTOR LAB CATALOG — CONFIRMED-V1

GET /api/v1/doctors/me/lab-catalog

Parameters:

search
pageNumber
pageSize

Search Active only.

Search fields include:

LOINC
Arabic/English names
ShortName
Aliases
Component

Use server-side search/paging.

Common orders rank higher.

---

71. REQUIRED ENDPOINTS — MANAGER LAB CATALOG — CONFIRMED-V1

GET  /api/v1/admin/lab-catalog
GET  /api/v1/admin/lab-catalog/{labTestId}

POST /api/v1/admin/lab-catalog
PUT  /api/v1/admin/lab-catalog/{labTestId}

POST /api/v1/admin/lab-catalog/{labTestId}/activate
POST /api/v1/admin/lab-catalog/{labTestId}/deactivate

POST /api/v1/admin/lab-catalog/{sourceLabTestId}/merge

Filters should support:

search
status
source
hasArabic
commonOnly
pageNumber
pageSize

---

72. REQUIRED ENDPOINTS — LAB IMPORT — CONFIRMED-V1

POST /api/v1/admin/lab-catalog/imports/preview

GET  /api/v1/admin/lab-catalog/imports
GET  /api/v1/admin/lab-catalog/imports/{batchId}
GET  /api/v1/admin/lab-catalog/imports/{batchId}/changes

POST /api/v1/admin/lab-catalog/imports/{batchId}/apply
POST /api/v1/admin/lab-catalog/imports/{batchId}/discard

Preview:

multipart/form-data
file = official ZIP

"changes" supports:

disposition
search
pageNumber
pageSize

---

73. REQUIRED ENDPOINTS — LAB CATALOG REQUESTS — CONFIRMED-V1

Doctor:

GET  /api/v1/doctors/me/lab-catalog-requests
GET  /api/v1/doctors/me/lab-catalog-requests/{requestId}

POST /api/v1/doctors/me/lab-catalog-requests
PUT  /api/v1/doctors/me/lab-catalog-requests/{requestId}

Manager:

GET  /api/v1/admin/lab-catalog-requests
GET  /api/v1/admin/lab-catalog-requests/{requestId}

POST /api/v1/admin/lab-catalog-requests/{requestId}/request-more-info
POST /api/v1/admin/lab-catalog-requests/{requestId}/approve
POST /api/v1/admin/lab-catalog-requests/{requestId}/reject

---

74. REQUIRED ENDPOINTS — LAB DURING ENCOUNTER — CONFIRMED-V1

GET /api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/lab-request

Returns null/no-draft state safely if none exists.

Add:

POST /api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/lab-request/items

Accept exactly one:

labTestCatalogId
XOR
newLabTest

Update:

PUT /api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/lab-request/items/{itemId}

May update:

DoctorInstructions

but not test identity.

Remove:

DELETE /api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/lab-request/items/{itemId}?rowVersion=...

---

75. REQUIRED ENDPOINT — POST-VISIT LAB REQUEST — CONFIRMED-V1

POST /api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/lab-requests/post-visit

Require:

PostVisitReason
items[]

Optional:

PatientInstructions

Each item supports Catalog or DoctorSubmitted source.

Create request directly as Requested.

---

76. REQUIRED ENDPOINTS — DOCTOR LAB REQUEST MANAGEMENT — CONFIRMED-V1

GET /api/v1/doctors/me/lab-requests
GET /api/v1/doctors/me/lab-requests/{requestId}
GET /api/v1/doctors/me/lab-requests/{requestId}/history

List filters:

practiceId
patientId
encounterId
origin
status
fromUtc
toUtc
pageNumber
pageSize

Cancellation:

POST /api/v1/doctors/me/lab-requests/{requestId}/items/{itemId}/cancel

POST /api/v1/doctors/me/lab-requests/{requestId}/cancel-remaining

Require:

reason
rowVersion

---

77. REQUIRED ENDPOINT — DIRECT LAB RESULT — CONFIRMED-V1

POST /api/v1/doctors/me/lab-requests/{requestId}/results

Multipart.

Metadata:

coveredLabRequestItemIds[]
externalLaboratoryName?
externalReportDate?
attachments[]

At least:

1 covered item
1 attachment

---

78. REQUIRED ENDPOINTS — DOCTOR LAB RESULTS — CONFIRMED-V1

GET /api/v1/doctors/me/lab-results
GET /api/v1/doctors/me/lab-results/{resultId}

GET /api/v1/doctors/me/lab-results/{resultId}/versions
GET /api/v1/doctors/me/lab-results/{resultId}/versions/{versionNumber}

POST /api/v1/doctors/me/lab-results/{resultId}/corrections
POST /api/v1/doctors/me/lab-results/{resultId}/void

Filters:

patientId
practiceId
requestId
fromUtc
toUtc
pageNumber
pageSize

---

79. REQUIRED ENDPOINTS — DOCTOR LAB SUBMISSION INBOX — CONFIRMED-V1

GET /api/v1/doctors/me/lab-result-submissions
GET /api/v1/doctors/me/lab-result-submissions/{submissionId}

POST /api/v1/doctors/me/lab-result-submissions/{submissionId}/accept
POST /api/v1/doctors/me/lab-result-submissions/{submissionId}/reject

Accept request:

{
  "coveredLabRequestItemIds": [],
  "rowVersion": "..."
}

Reject:

{
  "patientVisibleReason": "...",
  "rowVersion": "..."
}

---

80. REQUIRED ENDPOINTS — PATIENT LAB — CONFIRMED-V1

GET /api/v1/lab-requests/mine
GET /api/v1/lab-requests/mine/{requestId}

GET /api/v1/lab-results/mine
GET /api/v1/lab-results/mine/{resultId}

Patient gets only own issued/current clinical information.

---

81. REQUIRED ENDPOINTS — PATIENT LAB SUBMISSION — CONFIRMED-V1

POST /api/v1/lab-requests/mine/{requestId}/submissions

GET /api/v1/lab-requests/mine/{requestId}/submissions
GET /api/v1/lab-result-submissions/mine/{submissionId}

POST /api/v1/lab-result-submissions/mine/{submissionId}/withdraw

Patient submission create accepts:

externalLaboratoryName?
externalReportDate?
patientNote?
attachments[]

No coveredItemIds from Patient.

---

82. REQUIRED ENDPOINTS — LAB PRIVATE MEDIA — CONFIRMED-V1

Doctor official result:

GET /api/v1/doctors/me/lab-results/{resultId}/versions/{versionNumber}/attachments/{attachmentId}/content

Doctor Patient-submission review:

GET /api/v1/doctors/me/lab-result-submissions/{submissionId}/attachments/{attachmentId}/content

Patient own submission:

GET /api/v1/lab-result-submissions/mine/{submissionId}/attachments/{attachmentId}/content

Patient current result:

GET /api/v1/lab-results/mine/{resultId}/attachments/{attachmentId}/content

No public medical URLs.

---

83. REQUIRED ENDPOINTS — RADIOLOGY CATALOG — CONFIRMED-V1

Doctor:

GET /api/v1/doctors/me/radiology-catalog

Manager:

GET  /api/v1/admin/radiology-catalog
GET  /api/v1/admin/radiology-catalog/{procedureId}

POST /api/v1/admin/radiology-catalog
PUT  /api/v1/admin/radiology-catalog/{procedureId}

POST /api/v1/admin/radiology-catalog/{procedureId}/activate
POST /api/v1/admin/radiology-catalog/{procedureId}/deactivate

POST /api/v1/admin/radiology-catalog/{sourceProcedureId}/merge

---

84. REQUIRED ENDPOINTS — RADIOLOGY IMPORT — CONFIRMED-V1

POST /api/v1/admin/radiology-catalog/imports/preview

GET /api/v1/admin/radiology-catalog/imports
GET /api/v1/admin/radiology-catalog/imports/{batchId}
GET /api/v1/admin/radiology-catalog/imports/{batchId}/changes

POST /api/v1/admin/radiology-catalog/imports/{batchId}/apply
POST /api/v1/admin/radiology-catalog/imports/{batchId}/discard

Same official LOINC ZIP may be used.

---

85. REQUIRED ENDPOINTS — RADIOLOGY CATALOG REQUESTS — CONFIRMED-V1

Doctor:

GET  /api/v1/doctors/me/radiology-catalog-requests
GET  /api/v1/doctors/me/radiology-catalog-requests/{requestId}
POST /api/v1/doctors/me/radiology-catalog-requests
PUT  /api/v1/doctors/me/radiology-catalog-requests/{requestId}

Manager:

GET  /api/v1/admin/radiology-catalog-requests
GET  /api/v1/admin/radiology-catalog-requests/{requestId}

POST /api/v1/admin/radiology-catalog-requests/{requestId}/request-more-info
POST /api/v1/admin/radiology-catalog-requests/{requestId}/approve
POST /api/v1/admin/radiology-catalog-requests/{requestId}/reject

---

86. REQUIRED ENDPOINTS — RADIOLOGY DURING ENCOUNTER — CONFIRMED-V1

GET /api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/radiology-request

POST /api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/radiology-request/items

PUT /api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/radiology-request/items/{itemId}

DELETE /api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/radiology-request/items/{itemId}?rowVersion=...

Same Draft rules as Lab.

---

87. REQUIRED ENDPOINT — POST-VISIT RADIOLOGY — CONFIRMED-V1

POST /api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/radiology-requests/post-visit

Same rules as Lab.

---

88. REQUIRED ENDPOINTS — DOCTOR RADIOLOGY REQUESTS — CONFIRMED-V1

GET /api/v1/doctors/me/radiology-requests
GET /api/v1/doctors/me/radiology-requests/{requestId}
GET /api/v1/doctors/me/radiology-requests/{requestId}/history

POST /api/v1/doctors/me/radiology-requests/{requestId}/items/{itemId}/cancel
POST /api/v1/doctors/me/radiology-requests/{requestId}/cancel-remaining

---

89. REQUIRED ENDPOINT — DIRECT RADIOLOGY RESULT — CONFIRMED-V1

POST /api/v1/doctors/me/radiology-requests/{requestId}/results

Multipart:

coveredRadiologyRequestItemIds[]
externalRadiologyCenterName?
externalReportDate?
attachments[]

Each attachment includes:

kind = Report | Image

---

90. REQUIRED ENDPOINTS — DOCTOR RADIOLOGY RESULTS — CONFIRMED-V1

GET /api/v1/doctors/me/radiology-results
GET /api/v1/doctors/me/radiology-results/{resultId}

GET /api/v1/doctors/me/radiology-results/{resultId}/versions
GET /api/v1/doctors/me/radiology-results/{resultId}/versions/{versionNumber}

POST /api/v1/doctors/me/radiology-results/{resultId}/corrections
POST /api/v1/doctors/me/radiology-results/{resultId}/void

---

91. REQUIRED ENDPOINTS — DOCTOR RADIOLOGY SUBMISSIONS — CONFIRMED-V1

GET /api/v1/doctors/me/radiology-result-submissions
GET /api/v1/doctors/me/radiology-result-submissions/{submissionId}

POST /api/v1/doctors/me/radiology-result-submissions/{submissionId}/accept
POST /api/v1/doctors/me/radiology-result-submissions/{submissionId}/reject

---

92. REQUIRED ENDPOINTS — PATIENT RADIOLOGY — CONFIRMED-V1

GET /api/v1/radiology-requests/mine
GET /api/v1/radiology-requests/mine/{requestId}

GET /api/v1/radiology-results/mine
GET /api/v1/radiology-results/mine/{resultId}

---

93. REQUIRED ENDPOINTS — PATIENT RADIOLOGY SUBMISSION — CONFIRMED-V1

POST /api/v1/radiology-requests/mine/{requestId}/submissions

GET /api/v1/radiology-requests/mine/{requestId}/submissions
GET /api/v1/radiology-result-submissions/mine/{submissionId}

POST /api/v1/radiology-result-submissions/mine/{submissionId}/withdraw

---

94. REQUIRED ENDPOINTS — RADIOLOGY PRIVATE MEDIA — CONFIRMED-V1

GET /api/v1/doctors/me/radiology-results/{resultId}/versions/{versionNumber}/attachments/{attachmentId}/content

GET /api/v1/doctors/me/radiology-result-submissions/{submissionId}/attachments/{attachmentId}/content

GET /api/v1/radiology-result-submissions/mine/{submissionId}/attachments/{attachmentId}/content

GET /api/v1/radiology-results/mine/{resultId}/attachments/{attachmentId}/content

---

95. EXTEND ENCOUNTER RESPONSE — CONFIRMED-V1

Existing Doctor Encounter details must include:

labRequestDraft?
radiologyRequestDraft?

labRequestsSummary
radiologyRequestsSummary

completionBlockers

capabilities:
  canManageLabRequest
  canManageRadiologyRequest
  canCreatePostVisitLabRequest
  canCreatePostVisitRadiologyRequest

Do not force frontend to infer clinical state.

---

96. CAPABILITIES CONTRACT — CONFIRMED-V1

Mutable/read responses should expose server-derived capabilities.

Examples:

Catalog:

canEdit
canActivate
canDeactivate
canMerge

Import:

canApply
canDiscard

Catalog request:

canEdit
canRequestMoreInfo
canApprove
canReject

Diagnostic Request:

canManageDraft
canCancelRemaining
canUploadResult

Item:

canEdit
canRemove
canCancel
hasCurrentResult

Patient submission:

canWithdraw

Doctor submission:

canAccept
canReject

Result:

canCorrect
canVoid

Capabilities MUST be derived server-side from actor/resource state.

---

97. CONCURRENCY — CONFIRMED-V1

Use SQL Server "rowversion" following existing Wasla conventions.

Candidates:

Medical catalog
Catalog request
Import batch

LabRequest
LabResult
PatientLabResultSubmission

RadiologyRequest
RadiologyResult
PatientRadiologyResultSubmission

Prefer root-level concurrency for aggregate mutations.

Stale token:

409 Conflict

Do not silently overwrite.

---

98. IDEMPOTENCY — CONFIRMED-V1

Require "Idempotency-Key" for at least:

Catalog Merge
Import Apply
Catalog Request Approve

DoctorSubmitted Add Item
PostVisit Request creation

Patient Submission creation
Patient Submission Accept

Direct Result Upload
Result Correction
Result Void

and Radiology counterparts.

Same actor/operation/key/payload:

same logical response

Same key + changed payload:

409

Reuse Phase 14 idempotency implementation/pattern.

---

99. DATABASE CONSTRAINTS — CONFIRMED-V1

Enforce critical invariants at DB level where practical.

At minimum:

at most one Draft LabRequest per MedicalEncounter

at most one Draft RadiologyRequest per MedicalEncounter

Use filtered unique indexes or equivalent safe design.

Item source check:

CatalogId XOR CatalogRequestId

Catalog:

unique LOINC code when applicable

Result:

unique ResultId + VersionNumber

Merge:

MergedIntoId != self

Indexes needed for:

Doctor + Status
Patient + Status
Practice + Status
Encounter
Request
SubmittedAtUtc
RequestedAtUtc
LOINC code
catalog status/source
normalized search fields

---

100. AUDIT / IMMUTABLE HISTORY — CONFIRMED-V1

Generic audit fields are not enough.

Maintain explicit history for:

Catalog create/update/lifecycle/merge
Imports
Catalog review requests

Request publication
PostVisit creation
Cancellation

Patient submission transitions

Result upload
Correction
Void

Every sensitive mutation records:

ActorUserId
Actor type
OccurredAtUtc
Scope/resource
Reason where applicable
meaningful before/after state

Do not hard-delete completed clinical history.

---

101. LOGGING / PHI — CONFIRMED-V1

Do not log in ordinary Serilog/request logs:

Lab report contents
Radiology images
medical attachment bytes
full clinical request bodies containing PHI
clinical notes

Safe technical logs may contain:

CorrelationId
Operation
ResourceId
ErrorCode
Duration

Clinical access audit is separate.

---

102. ERROR CODES — LAB CATALOG — CONFIRMED-V1

Implement stable localized ProblemDetails codes following existing Wasla style.

Examples:

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

Radiology equivalent.

---

103. ERROR CODES — IMPORT — CONFIRMED-V1

DiagnosticCatalogImport.PackageTooLarge
DiagnosticCatalogImport.InvalidArchive
DiagnosticCatalogImport.UnsafeArchive
DiagnosticCatalogImport.RequiredFileMissing
DiagnosticCatalogImport.InvalidHeaders
DiagnosticCatalogImport.UnsupportedSourceVersion
DiagnosticCatalogImport.BatchNotFound
DiagnosticCatalogImport.BatchNotStaged
DiagnosticCatalogImport.AlreadyApplied
DiagnosticCatalogImport.AlreadyDiscarded
DiagnosticCatalogImport.ConcurrencyConflict

---

104. ERROR CODES — CATALOG REQUEST — CONFIRMED-V1

Examples:

LabCatalogRequest.NotFound
LabCatalogRequest.InvalidState
LabCatalogRequest.NotOwner
LabCatalogRequest.MoreInfoReasonRequired
LabCatalogRequest.RejectionReasonRequired
LabCatalogRequest.InvalidDuplicateTarget
LabCatalogRequest.ConcurrencyConflict

Radiology equivalent.

---

105. ERROR CODES — DIAGNOSTIC REQUEST — CONFIRMED-V1

Examples:

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

Radiology equivalent.

---

106. ERROR CODES — SUBMISSION — CONFIRMED-V1

LabResultSubmission.NotFound
LabResultSubmission.NotOwner
LabResultSubmission.NotPending
LabResultSubmission.NoRequestedItems
LabResultSubmission.InvalidCoverage
LabResultSubmission.RejectionReasonRequired
LabResultSubmission.NoLongerApplicable
LabResultSubmission.ConcurrencyConflict

Radiology equivalent.

---

107. ERROR CODES — RESULT — CONFIRMED-V1

LabResult.NotFound
LabResult.AttachmentRequired
LabResult.CoverageRequired
LabResult.InvalidCoverage
LabResult.ItemNotRequested
LabResult.NoCurrentVersion
LabResult.InvalidCorrectionState
LabResult.InvalidVoidState
LabResult.VoidReasonRequired
LabResult.ConcurrencyConflict

Radiology equivalent.

Reuse existing BuildingBlock media error codes where appropriate.

---

108. HTTP BEHAVIOR — CONFIRMED-V1

Follow Wasla conventions.

Conceptually:

400 malformed HTTP/request contract

401 unauthenticated

403 actor/permission/state denied

404 safe not-found for foreign/inaccessible clinical resource

409 concurrency/state/idempotency race

413 request/file/package too large

422 business/input validation

No stack traces/database internals.

Localized ar/en human messages.

Stable machine-readable codes.

---

109. RESPONSE DESIGN — CONFIRMED-V1

Do not return only IDs after important mutations.

Return updated authoritative state.

Examples:

Add item:

LabRequestStateResponse

Cancel:

LabRequestStateResponse

Accept submission:

submission
result
updated request

Correction/Void:

updated result
updated request state where affected

Frontend should replace local state from response.

---

110. TESTING — UNIT — CONFIRMED-V1

Add focused domain/validator tests for:

one Draft per Encounter
Draft publication
PostVisit request
duplicate item prevention
Catalog/DoctorSubmitted XOR
cancel item
cancel remaining

request status recalculation

Patient submission:
Accept
Reject
Withdraw
NoLongerApplicable

result coverage
result correction
result void
item reopening after void

catalog lifecycle
merge rules
catalog request lifecycle

Both Lab and Radiology.

---

111. TESTING — AUTHORIZATION / SECURITY — CONFIRMED-V1

Must verify:

Doctor A cannot access Doctor B diagnostic request/result

same Practice does not bypass requesting Doctor ownership

Reception denied Lab/Radiology clinical access

Ordinary SuperAdmin denied Lab/Radiology clinical data

MedicalCatalogManager denied Patient clinical data

Patient A denied Patient B resources

Suspended Doctor denied clinical access

Root account governance does not grant clinical access

Patient cannot choose result coverage

---

112. TESTING — INTEGRATION — CONFIRMED-V1

Must cover:

Complete Visit atomically publishes diagnostic Drafts

CatalogRequest + DoctorSubmitted item atomicity

PostVisit creation atomicity

Patient Submission Accept atomicity

Result correction transaction

Result void transaction

RowVersion conflicts

Idempotency replay

DB source XOR constraint

one Draft DB constraint

private media authorization

unsupported file type

file signature mismatch

file size policy

---

113. TESTING — IMPORT — CONFIRMED-V1

Do NOT commit the full LOINC 2.83 ZIP merely for tests.

Create small realistic fixtures reflecting its actual schema.

Test:

CLASSTYPE = 1 filtering
ORDER_OBS Order/Both filtering

ACTIVE selectable
non-active non-selectable

Universal Lab Orders ranking

Arabic present
Arabic absent

Radiology grouping by LoincNumber

source change classification

Wasla-only possible conflict

preview without runtime mutation

apply
apply idempotent retry

discard

invalid ZIP
unsafe ZIP path
missing CSV
invalid headers
oversized package

---

114. MIGRATION — CONFIRMED-V1

Create controlled EF migration for all Phase 15 persistence.

Review generated migration manually.

Do not enable automatic production migration behavior.

Add all required:

tables
FKs
filtered unique indexes
check constraints
search indexes
rowversion columns

No destructive modification of Phase 13/14 clinical history.

---

115. PERFORMANCE — CONFIRMED-V1

Do not:

load entire LOINC catalog into memory for normal runtime searches
filter full tables application-side
cause N+1 in catalog/search/inbox lists

Use server-side specifications/projections/paging.

Import processing may stream/stage data appropriately.

Doctor catalog search must remain responsive for tens of thousands of rows.

---

116. CACHE — CONFIRMED-V1

Catalog/search data may use caching only if consistent with existing platform patterns.

Any mutation/import/activate/deactivate/merge must invalidate affected cache.

Do NOT globally cache Patient clinical responses.

---

117. NOTIFICATIONS — CONFIRMED-V1

Do not expand the full notification system in this Phase.

Keep future events possible for:

PatientResultSubmitted
OfficialResultAvailable
PatientSubmissionRejected

No clinical command should fail because email/SMS delivery failed.

If notification side effects are implemented using existing approved patterns, use durable Outbox.

Do not invent notification channels.

---

118. OUT OF SCOPE — CONFIRMED-V1

Do NOT implement:

Laboratory actor/account
Radiology Center actor/account

Lab marketplace
Radiology marketplace

assigning request to Lab
assigning request to Radiology Center

direct provider integrations

DICOM
PACS
DICOM Viewer

structured numeric Lab results
reference-range evaluation
abnormal-flag computation

automatic clinical interpretation
AI diagnosis

cross-Doctor medical history sharing
clinical reassignment to another Doctor

family/guardian clinical access
emergency clinical access

SuperAdmin clinical support override

Central Patient Timeline / Phase 16

insurance

full notification expansion

Do not add speculative abstractions for these.

---

119. DO NOT CREATE GENERIC DIAGNOSTIC MEGA-DOMAIN — CONFIRMED-V1

Do not introduce something like:

DiagnosticRequest<T>
DiagnosticResult<T>

as a generic aggregate framework.

Lab and Radiology have parallel but distinct Domain models.

Share:

technical helpers
media helpers
utility abstractions
common application patterns

only where this clearly reduces duplication without obscuring business language.

---

120. SOURCE / LICENSE COMPLIANCE — CONFIRMED-V1

Preserve external source/license information necessary for compliance.

Do not alter official source terminology and present it as official LOINC terminology.

Do not commit a massive copied LOINC dataset into source-control migrations.

Document that runtime catalog content may originate from official LOINC release import.

Keep:

SourceVersion
source code
copyright notice/link

where appropriate.

Add the required attribution/license notice/documentation in the appropriate repository documentation.

Do not claim Wasla owns LOINC/RSNA terminology.

---

121. REQUIRED DOCUMENTATION OUTPUT — CONFIRMED-V1

Create:

SOURCE-OF-TRUTH-PHASE-15-ADDENDUM.md

PHASE-15-API-CHANGES.md

PHASE-15-CLOSURE-REPORT.md

"PHASE-15-API-CHANGES.md" must include:

all new routes
changed existing routes
request DTOs
response DTOs
pagination/filter behavior
capabilities
rowVersion behavior
Idempotency-Key requirements
file upload format
frontend retry rules
error codes

Frontend must be able to integrate using this file without reading backend code.

---

122. README UPDATE — CONFIRMED-V1

Only after Phase completion:

Update current stage to:

Phase 15 — Diagnostic Orders & Results — DONE

Add summary of:

MedicalCatalogManager
LOINC Lab Catalog
LOINC/RSNA Radiology Catalog
Lab/Radiology Requests
Patient submissions
Doctor-reviewed results
Private medical media
Result correction/void

Mention exact final tests passed based on real output.

Do not invent test counts.

---

123. BUILD / TEST EXPECTATION — CONFIRMED-V1

At minimum run:

dotnet build

Then run Phase 15 focused:

Unit
Integration
Architecture

tests.

Also run affected regression areas:

Authentication/permissions
Root governance
MedicalEncounter
Complete Visit
Prescription finalization
Patient clinical access
Media
Idempotency

Before marking Phase DONE, run the project's complete test suite if practical in the repository environment.

Do not suppress failures.

Do not change unrelated tests merely to obtain green output.

---

124. DEFINITION OF DONE — CONFIRMED-V1

Do not mark Phase 15 completed until:

[ ] Source-of-Truth Phase 15 addendum exists

[ ] MedicalCatalogManager governance implemented

[ ] permissions seeded

[ ] Lab catalog implemented

[ ] LOINC Lab import implemented

[ ] Arabic partial-source strategy implemented

[ ] common Lab order ranking implemented

[ ] LabCatalogRequest lifecycle implemented

[ ] LabRequest Draft/PostVisit implemented

[ ] Complete Visit integration implemented

[ ] Lab cancellation implemented

[ ] Patient Lab submission implemented

[ ] Doctor Lab review inbox implemented

[ ] Official LabResult implemented

[ ] Result correction/void implemented

[ ] Patient Lab views implemented

[ ] Lab private media implemented

[ ] Radiology catalog implemented

[ ] LOINC/RSNA import implemented

[ ] RadiologyCatalogRequest implemented

[ ] RadiologyRequest implemented

[ ] Patient Radiology submission implemented

[ ] Doctor Radiology review implemented

[ ] Radiology Result implemented

[ ] Radiology correction/void implemented

[ ] Patient Radiology views implemented

[ ] Radiology private media implemented

[ ] RowVersion conflicts covered

[ ] Idempotency covered

[ ] DB constraints/indexes implemented

[ ] Migration reviewed

[ ] stable ProblemDetails/error codes implemented

[ ] localization ar/en added

[ ] sensitive audit/history added

[ ] PHI-safe logging preserved

[ ] API changes document completed

[ ] focused tests pass

[ ] affected regression tests pass

[ ] solution builds cleanly

[ ] closure report records actual verification

[ ] README updated only after real completion

---

125. FINAL IMPLEMENTATION PRINCIPLE — CONFIRMED-V1

Preserve this trust chain:

External report
        ↓
Patient may submit it
        ↓
but Patient submission is NOT an Official Result
        ↓
same Requesting Doctor reviews
        ↓
Doctor accepts/records it
        ↓
Official clinical Result
        ↓
immutable/correctable history
        ↓
Patient sees current valid result

And preserve the separation:

Catalog Manager
= Reference Data Authority

Doctor
= Clinical Authority for own diagnostic orders/results

Patient
= Owner/viewer/uploader of own documents

Reception
= Operational actor, not Clinical

SuperAdmin
= Platform governance, not automatic Clinical authority

---

126. DELIVERY FORMAT — CONFIRMED-V1

Work through the implementation in dependency order.

Do not stop after scaffolding.

When finished provide:

1. Implementation summary — CONFIRMED-V1
2. Important domain decisions implemented — CONFIRMED-V1
3. Files/modules added or changed — CONFIRMED-V1
4. Migration name — CONFIRMED-V1
5. Endpoint summary — CONFIRMED-V1
6. Permission summary — CONFIRMED-V1
7. LOINC import implementation summary — CONFIRMED-V1
8. Security/concurrency/idempotency summary — CONFIRMED-V1
9. Test/build commands actually run — CONFIRMED-V1
10. Exact pass/fail results — CONFIRMED-V1
11. Anything intentionally deferred — CONFIRMED-V1
12. Any deviation from this task and why — CONFIRMED-V1

Do not mark something DONE if it was only designed/scaffolded.

If an existing Wasla convention conflicts technically with a suggested class/route shape, preserve the approved business behavior, follow the existing project convention, and document the adaptation in the closure report.

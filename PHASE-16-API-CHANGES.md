# Phase 16 frontend API contract and route status

**2026-10-24 final business-policy update:** all 29 method/route signatures below
are retained. P16-24–29 and the existing family roster/status now authorize
Father/Mother through exact active parent-child verification without medical
eligibility. Guardian/LegalGuardian still require current exact authority proof
and Approved medical eligibility. See [the exact final diff and frontend handoff](PHASE-16-FINAL-GAP-API-DIFF.md)
and [the approved business incorporation addendum](SOURCE-OF-TRUTH-PHASE-16-FINAL-BUSINESS-ADDENDUM.md).

Local backend implementation; **production sharing is not approved**. The full
master v1.2 is not checked in. Read the proposed addendum and rollout gates.
All 29 routes have working handlers. Both sharing flags are true in base configuration;
Production and Staging disable both. Guardian routes additionally require approved
policy configuration and role-specific exact proof/eligibility; base policy fails closed.
See [final closure](PHASE-16-CLOSURE-REPORT.md) for current verification.

All routes use API version 1, authenticated actors, current DB permissions and
server-derived subjects. No self route accepts PatientId. Every treating route
requires the exact own Ticket AND matching Encounter InProgress, active owned
practice, approved doctor and active non-first-login account. A reservation-only,
Waiting or Called context has no clinical access. Both ticket origins qualify.

## Exact route inventory

The response column references the complete templates below. Every row uses the
common ProblemDetails/error rules. Disabled sharing returns safe 404 regardless
of resource existence. Gated does not mean approved for production.

| ID | HTTP | Route | Permission | Response template | Status |
|---|---|---|---|---|---|
| P16-01 | GET | `/api/v1/patients/me/medical-record/overview` | `MedicalRecords.ViewOwn` | Overview | **IMPLEMENTED** |
| P16-02 | GET | `/api/v1/patients/me/medical-record/timeline` | `MedicalRecords.ViewOwn` | Timeline page | **IMPLEMENTED** |
| P16-03 | GET | `/api/v1/patients/me/medical-record/encounters/{encounterId}` | `MedicalRecords.ViewOwn` | Encounter details | **IMPLEMENTED** |
| P16-04 | GET | `/api/v1/patients/me/medical-record/items/{resourceType}/{sourceId}` | `MedicalRecords.ViewOwn` | Item details | **IMPLEMENTED** |
| P16-05 | GET | `/api/v1/patients/me/medical-record/items/{resourceType}/{sourceId}/attachments/{attachmentId}/content` | `MedicalRecords.ViewOwn` | Private bytes | **IMPLEMENTED** |
| P16-06 | GET | `/api/v1/patients/me/medical-record/access-log` | `MedicalRecords.ViewOwnAccessLog` | Access-log page | **IMPLEMENTED** |
| P16-07 | GET | `/api/v1/doctors/me/practices/{practiceId}/tickets/{ticketId}/medical-record/overview` | `MedicalRecords.ViewTreatingPatient` | Overview | **GATED (implemented)** |
| P16-08 | GET | `/api/v1/doctors/me/practices/{practiceId}/tickets/{ticketId}/medical-record/timeline` | `MedicalRecords.ViewTreatingPatient` | Timeline page | **GATED (implemented)** |
| P16-09 | GET | `/api/v1/doctors/me/practices/{practiceId}/tickets/{ticketId}/medical-record/encounters/{encounterId}` | `MedicalRecords.ViewTreatingPatient` | Encounter details | **GATED (implemented)** |
| P16-10 | GET | `/api/v1/doctors/me/practices/{practiceId}/tickets/{ticketId}/medical-record/items/{resourceType}/{sourceId}` | `MedicalRecords.ViewTreatingPatient` | Item details | **GATED (implemented)** |
| P16-11 | GET | `/api/v1/doctors/me/practices/{practiceId}/tickets/{ticketId}/medical-record/items/{resourceType}/{sourceId}/attachments/{attachmentId}/content` | `MedicalRecords.ViewTreatingPatient` | Private bytes | **GATED (implemented)** |
| P16-12 | GET | `/api/v1/doctors/me/practices/{practiceId}/tickets/{ticketId}/medical-record/access-requests` | `HiddenEncounterAccessRequests.ViewSent` | Request page | **GATED (implemented)** |
| P16-13 | GET | `/api/v1/doctors/me/practices/{practiceId}/tickets/{ticketId}/medical-record/access-requests/{requestId}` | `HiddenEncounterAccessRequests.ViewSent` | Requester request | **GATED (implemented)** |
| P16-14 | POST | `/api/v1/doctors/me/practices/{practiceId}/tickets/{ticketId}/medical-record/access-requests` | `HiddenEncounterAccessRequests.Create` | Requester request (201) | **GATED (implemented)** |
| P16-15 | GET | `/api/v1/doctors/me/medical-record-access-requests/inbox/summary` | `HiddenEncounterAccessRequests.ViewIncoming` | Inbox summary | **GATED (implemented)** |
| P16-16 | GET | `/api/v1/doctors/me/medical-record-access-requests/inbox` | `HiddenEncounterAccessRequests.ViewIncoming` | Owner request page | **GATED (implemented)** |
| P16-17 | GET | `/api/v1/doctors/me/medical-record-access-requests/{requestId}` | `HiddenEncounterAccessRequests.ViewIncoming` | Owner request | **GATED (implemented)** |
| P16-18 | POST | `/api/v1/doctors/me/medical-record-access-requests/{requestId}/approve` | `HiddenEncounterAccessRequests.DecideOwn` | Owner request | **GATED (implemented)** |
| P16-19 | POST | `/api/v1/doctors/me/medical-record-access-requests/{requestId}/reject` | `HiddenEncounterAccessRequests.DecideOwn` | Owner request | **GATED (implemented)** |
| P16-20 | POST | `/api/v1/doctors/me/medical-record-access-requests/{requestId}/revoke` | `HiddenEncounterAccessRequests.RevokeOwn` | Owner request | **GATED (implemented)** |
| P16-21 | GET | `/api/v1/doctors/me/medical-record-access-requests/{requestId}/history` | `HiddenEncounterAccessRequests.ViewHistoryOwn` | Request history | **GATED (implemented)** |
| P16-22 | PATCH | `/api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/visibility` | `MedicalEncounterVisibility.UpdateOwn` | Visibility | **IMPLEMENTED** |
| P16-23 | GET | `/api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/visibility/history` | `MedicalEncounterVisibility.ViewHistoryOwn` | Visibility-history page | **IMPLEMENTED** |
| P16-24 | GET | `/api/v1/families/mine/medical-record-dependents` | `MedicalRecords.ViewDependent` | Eligible dependent page | **GATED (implemented)** |
| P16-25 | GET | `/api/v1/families/mine/dependents/{dependentPatientId}/medical-record/overview` | `MedicalRecords.ViewDependent` | Overview | **GATED (implemented)** |
| P16-26 | GET | `/api/v1/families/mine/dependents/{dependentPatientId}/medical-record/timeline` | `MedicalRecords.ViewDependent` | Timeline page | **GATED (implemented)** |
| P16-27 | GET | `/api/v1/families/mine/dependents/{dependentPatientId}/medical-record/encounters/{encounterId}` | `MedicalRecords.ViewDependent` | Encounter details | **GATED (implemented)** |
| P16-28 | GET | `/api/v1/families/mine/dependents/{dependentPatientId}/medical-record/items/{resourceType}/{sourceId}` | `MedicalRecords.ViewDependent` | Item details | **GATED (implemented)** |
| P16-29 | GET | `/api/v1/families/mine/dependents/{dependentPatientId}/medical-record/items/{resourceType}/{sourceId}/attachments/{attachmentId}/content` | `MedicalRecords.ViewDependent` | Private bytes | **GATED (implemented)** |

| Extension | Existing route | Additive behavior | Status |
|---|---|---|---|
| EXT-01 | POST `/api/v1/practices/{practiceId}/tickets/{ticketId}/complete` | Optional visibility, committed with completion, default new Visible. Existing rowversions/idempotency retained. | IMPLEMENTED |
| EXT-02 | GET `/api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}` | visibility, visibilityRevision, canChangeVisibility and existing rowVersion. | IMPLEMENTED |
| EXT-03 | GET `/api/v1/doctors/me/practices/{practiceId}/encounters` | visibility on each row; explicit OpenAPI list schema. | IMPLEMENTED |

Permissions are appended to PermissionNames.All to preserve position-derived
IDs. Doctor receives ViewTreatingPatient, both visibility permissions and all six
request permissions. Patient receives ViewOwn, ViewOwnAccessLog and ViewDependent
(ViewDependent additionally requires approved policy, exact relationship and role-specific eligibility).
Reception/SuperAdmin/catalog roles receive none of these permissions by default.
The existing seed is repeatable. Ownership/relationship checks still apply to
explicit permission grants; permissions or GUIDs alone never grant clinical access.

## Query, serialization and errors

Timeline/overview: `types=all|visits|diagnoses|prescriptions|labs|radiology`, also
comma-separated; `fromDate`, `toDate`, `cursor`, `pageSize` (default 20, 1–100).
Clinical date boundaries are inclusive UTC dates (through next midnight exclusive).
Hidden visitDate is the original Ticket's practice business date; its ordering
uses only that date at midnight. Types always retain redacted placeholders and
never reveal their underlying item categories. `encounterId` is an additive
filter for authorized encounter output paging; it cannot probe Hidden source IDs.
Encounter details include a first output page of 100. Follow its cursor on the
same actor timeline with `encounterId` and the same filters; an unusually large
encounter's named source lists reflect that first page and continue in outputs.

Cursors are authenticated/encrypted and scoped to actor, subject, ticket, filters
and current state. Timeline cursors have a one-hour lifetime/upper time bound.
Visibility/grant/clinical changes invalidate them; refetch page one on 422. List
cursors paginate current request/access-log metadata; fresh arrivals appear on
refresh. Empty authorized lists are 200 with items=[], hasMore=false, nextCursor=null.
No raw offset, storage key or PHI is exposed in cursors.

JSON uses camelCase and the existing string-enum converter; legacy numeric enum
inputs remain accepted. Visibility: Visible=1, Hidden=2. Request states: Pending=1,
Approved=2, Rejected=3, Revoked=4, Expired=5. DateOnly is ISO YYYY-MM-DD; new Phase 16
UTC timestamps carry Z. Typed resource whitelist is prescription, lab-request,
lab-result, radiology-request, radiology-result. Other types are validation errors.

401 means no authentication. 404 MedicalRecord.Unavailable sanitizes disabled,
unauthorized and missing scoped resources. 409 means a state/duplicate/rowversion
conflict. Malformed binding/JSON may be 400; application validation uses the
existing 422 convention. 429 is rate limiting; audit persistence failure returns
500 with no clinical body. OpenAPI lists response DTOs and ProblemDetails errors.

```json
{"type":"about:blank#not-found","title":"Not Found","status":404,
 "detail":"The medical record is unavailable.","traceId":"trace",
 "errors":[{"code":"MedicalRecord.Unavailable","message":"The medical record is unavailable.","type":"NotFound"}]}
```

Stable new codes: MedicalRecord.Unavailable, MedicalRecord.InvalidInput,
MedicalRecord.ConcurrencyConflict, HiddenAccessRequest.InvalidState,
HiddenAccessRequest.Expired, HiddenAccessRequest.AlreadyRequestedForTicket.
Messages follow the existing English/Arabic resources and Accept-Language.

## Overview and timeline templates

Patient/future dependent screens use PatientSafeMedicalRecord* responses;
treating screens use distinct DoctorSharedMedicalRecord* responses. Both share
only the safe central fields shown here; it is a separate contract from the original private owner encounter
DTO. `mode` distinguishes OwnPatient/TreatingDoctor/Dependent. Internal owner
notes, diagnosis Notes, amendment reasons and draft versions are absent.

```json
{"patient":{"patientId":"00000000-0000-0000-0000-000000000001","nameAr":"Example","nameEn":"Example","dateOfBirth":"2000-01-01","gender":"Male"},
 "context":{"mode":"OwnPatient","ticketId":null,"treatingEncounterId":null,"treatmentStatus":null},
 "summary":{"completedVisitCount":1,"currentPrescriptionCount":0,"officialLabResultCount":0,"officialRadiologyResultCount":0,"hiddenEncounterCount":0},
 "timeline":{"items":[],"nextCursor":null,"hasMore":false},
 "capabilities":{"canView":true,"canViewAttachments":true,"canRequestHiddenAccess":false},
 "availableFilters":["all","visits","diagnoses","prescriptions","labs","radiology"]}
```

Treating overview derives patient from Ticket, fills ticketId/treatingEncounterId
and InProgress status. Counts cover authorized outputs only. hiddenEncounterCount
counts placeholders, never their prescription/test/file counts. Capabilities
use current permissions (including Create for the request action).

```json
{"items":[
 {"type":"EncounterCompleted","entryId":"1:opaque-source-key","occurredAtUtc":"2026-10-01T10:30:00Z","sourceId":"00000000-0000-0000-0000-000000000002","encounterId":"00000000-0000-0000-0000-000000000002","resourceType":"encounter","status":"Current","title":"Completed visit","capabilities":{"canViewDetails":true,"canViewAttachments":false}},
 {"type":"HiddenEncounterPlaceholder","entryId":"opaque-stable-hmac","visitDate":"2026-09-01","isHidden":true,"hiddenEncounterToken":"protected-token","accessRequest":{"status":"NotRequested","requestId":null},"capabilities":{"canRequestAccess":true}}
],"nextCursor":null,"hasMore":false}
```

Clinical types: EncounterCompleted, DiagnosisCurrent, PrescriptionFinalized,
LabRequestIssued, LabResultPublished, RadiologyRequestIssued,
RadiologyResultPublished. Visit/diagnosis entries navigate to encounter details;
other entries navigate through their resourceType/sourceId. Result events use
actual review/publish time, including post-visit outputs. Only current finalized
prescriptions and official current reviewed results appear as active outputs.
Superseded, draft and voided active representations are excluded.

Hidden is a distinct serialized union with exactly type, entryId, visitDate,
isHidden, hiddenEncounterToken, accessRequest and capabilities. It never has
occurredAtUtc/sourceId/encounterId/title/doctor/practice/diagnoses/attachments,
including nested or null versions of those fields. OpenAPI describes the union.

## Encounter and item templates

```json
{"encounterId":"00000000-0000-0000-0000-000000000002","visitDate":"2026-10-01",
 "doctor":{"id":"00000000-0000-0000-0000-000000000003","nameAr":"Example","nameEn":"Example"},
 "practice":{"id":"00000000-0000-0000-0000-000000000004","nameAr":"Example","nameEn":"Example"},
 "diagnoses":[{"diagnosisId":"00000000-0000-0000-0000-000000000005","type":"Primary","displayText":"Example diagnosis"}],
 "outputs":{"items":[],"nextCursor":null,"hasMore":false},
 "capabilities":{"canView":true,"canViewAttachments":true,"canRequestHiddenAccess":false},
 "prescription":null,"labRequests":[],"labResults":[],"radiologyRequests":[],"radiologyResults":[]}
```

The named source lists are safe timeline references, not source aggregates. Their
detail and attachment metadata come from items. Current diagnoses contain no
Notes. Own Hidden encounters are visible to the verified patient; treating
Hidden details require the exact effective approved grant (or source ownership).
The original owner's private consultation APIs remain available for own work.

Privacy correction to the existing `/api/v1/encounters/mine/{encounterId}` DTO:
`currentActiveDiagnoses[].notes` now returns null (or is omitted by null-value
serialization). Diagnosis IDs, types, display text and the response shape are
preserved. The owning Doctor's `diagnoses[].notes` remains private and populated.
The Phase 16 patient, treating-Doctor and dependent DTOs have no Notes field.

Dependent reads use the same patient-safe completed outputs, including Hidden
encounters; the separate Doctor-to-Doctor request workflow does not approve
Guardian access. P16-24 returns `{items:[{patientId,nameAr,nameEn,medicalAccess}],
nextCursor:null,hasMore:false}` for currently eligible children only. Permission,
verified exact relationship in the same active Family, no restriction and age
under 16 must all hold. Guardian/LegalGuardian additionally require Approved
eligibility tied to current memberships/policy/basis/authority proof; parents do
not require medical eligibility. Ineligible children are omitted from P16-24; direct reads
return `404 MedicalRecord.Unavailable`. An unavailable policy or permission denies
P16-24 itself. All clinical reads and downloads revalidate these requirements.

```json
{"resourceType":"lab-result","sourceId":"00000000-0000-0000-0000-000000000006","encounterId":"00000000-0000-0000-0000-000000000002","status":"Official","currentVersion":1,
 "safeContent":{"requestId":"00000000-0000-0000-0000-000000000007","reviewedAtUtc":"2026-10-02T10:00:00Z","externalProviderName":"Example provider","externalReportDate":"2026-10-02","coveredItemIds":["00000000-0000-0000-0000-000000000008"]},
 "attachments":[{"attachmentId":"00000000-0000-0000-0000-000000000009","fileName":"report.pdf","contentType":"application/pdf","sizeBytes":100,"canDownload":true}],
 "capabilities":{"canView":true,"canViewAttachments":true,"canRequestHiddenAccess":false}}
```

Prescription safeContent is `{items:[{sortOrder,medicationName,scientificName,
clinical:{strength,dosageForm,route,dose,frequencyCode,frequencyText,durationType,
durationValue,durationUnit,asNeeded,prnReason,minimumIntervalText,maxPer24HoursText,
quantityValue,quantityUnit,instructions}}]}`. It has no catalog request identifiers,
draft or correction/void reason. A direct current voided prescription returns
status Voided with empty items and is not counted as active.

Order safeContent is `{requestedAtUtc,patientInstructions,items:[{itemId,nameAr,
nameEn,code,instructions,status}],resultStatus}`. resultStatus is Official,
MissingResult, or PendingReview for the patient's own pending submission. Pending
files never become official until reviewed. Shared order output does not include
patient submissions, postVisitReason or cancellation/internal notes.

Result safeContent is the reviewed current version shown above (radiology uses
the same shape). Corrected results return status Corrected and their current
version/attachments. Previous or voided result attachments become unavailable.
OpenAPI describes the three safeContent variants. Downloads return private bytes,
validated MIME and sanitized Content-Disposition attachment filename, no-store
and nosniff headers. Range processing is disabled; download cap is 25 MiB. Every
call validates current actor/treatment/visibility/grant/current parent version.

## Request and owner inbox templates

Creation requires Idempotency-Key and body:

```json
{"hiddenEncounterToken":"protected-token-from-placeholder","reason":"Clinical justification"}
```

Reason is required, trimmed, nonblank and capped at 1,000 characters. It has no
invented clinical semantics. Creation returns 201 plus Location and current DTO.
Approve requires Idempotency-Key; reject/revoke do not. Decision body:

```json
{"rowVersion":"AAAAAAAAAAA="}
```

```json
{"requestId":"00000000-0000-0000-0000-000000000010","status":"Pending","effectiveStatus":"Pending","reason":"Clinical justification","requestedAtUtc":"2026-10-12T09:00:00Z","decidedAtUtc":null,"revokedAtUtc":null,"expiresWhenTicketEnds":true,"rowVersion":"AAAAAAAAAAA=","capabilities":{"canApprove":false,"canReject":false,"canRevoke":false,"canOpenGrantedRecord":false}}
```

Only active approved requester DTOs add grantedEncounterId and
canOpenGrantedRecord=true. Pending/Rejected/Revoked/Expired requester DTOs have no
ownerDetails or grantedEncounterId property. Owner detail and inbox list add
`ownerDetails:{patient:{id,nameAr,nameEn},encounterId,requestingDoctor:{id,nameAr,
nameEn},requestingPracticeId}`; the owner is already entitled to their own source.
Owner decision responses include this information and current capabilities.
A Pending live owner response allows approve/reject; an Approved live one allows
revoke. A stopped treating context or changed source revision is effectively
Expired even if its stored request status has not yet been cleaned up.

Lists: `{items:[<request DTO>],nextCursor,hasMore}`. Inbox filters: status,
practiceId, search (max 200), cursor, pageSize. Sent list filters: status, cursor,
pageSize, scoped to this Ticket. Inbox is global across owned active practices,
never dependent on a selected practice. Badge is
`{pendingCount:1,actionableCount:1,approvedActiveCount:0}` with no patient metadata.
History is `[{historyId,action,actorApplicationUserId,occurredAtUtc}]`, with no PHI.

SQL enforces one request forever per source encounter/ticket across all states.
A new key after an existing request returns 409 AlreadyRequestedForTicket.
Identical key and normalized payload replays a current safe response after fresh
authorization; changed payload conflicts under the existing medication
idempotency contract. Stale rowversions conflict. Reject/revoke repeated in the
same terminal state with a fresh matching version are safe no-ops; stale original
versions conflict. Requests do not reopen. No public Expire endpoint exists.

## Visibility and access-log templates

```json
{"visibility":"Hidden","rowVersion":"AAAAAAAAAAA="}
```

```json
{"encounterId":"00000000-0000-0000-0000-000000000002","visibility":"Hidden","visibilityRevision":3,"rowVersion":"AAAAAAAAAAA=","capabilities":{"canChangeVisibility":true}}
```

History: `{items:[{historyId,oldVisibility,newVisibility,revision,
actorApplicationUserId,occurredAtUtc,source}],nextCursor,hasMore}`. source is
Completion, OwnerChoice or LegacyMigration. Legacy baseline has oldVisibility=null,
revision=0 and actor=null. No-op choices do not increment revision or add history.
Visibility changes invalidate former approvals; toggling back cannot resurrect.

Complete body extends the existing contract:

```json
{"ticketRowVersion":"AAAAAAAAAAA=","encounterRowVersion":"AAAAAAAAAAA=","prescriptionRowVersion":null,"labRequestRowVersion":null,"radiologyRequestRowVersion":null,"visibility":"Hidden"}
```

Omission retains the new encounter's default and preserves old default-visible
idempotency fingerprints. Completion response remains the existing Ticket DTO.
The choice is committed atomically with encounter/ticket/prescription/diagnostic
issuance and request expiry. There is no Complete-then-PATCH visibility window.

Patient access-log: `{items:[{entryId,actorCategory,action,occurredAtUtc}],
nextCursor,hasMore}`. It contains Phase 16 audit metadata only: no free-text reason,
correlation IDs, requesting doctor identity, target resource IDs or raw clinical
payload. Retention and expanded patient disclosure policy remain governance gates.

## Seven frontend journeys

| Journey | Request sequence | Failure/action handling |
|---|---|---|
| A patient own record | Login → 01 → 02 → 03 → 04 → 05; 06 for access history | 200 empty state; own Hidden outputs visible; current correction/void semantics; source/attachment IDOR 404; audit outage 500. |
| B reservation treatment | Existing reservation check-in → existing StartVisit → 07 → 08 → 09/10/11 | Reservation alone/Waiting/Called denied; matching InProgress ticket+encounter required; close foreign record on completion. |
| C walk-in treatment | Existing walk-in → StartVisit → same 07–11 | Identical treatment policy independent of origin. |
| D hidden request | 08 date-only token → 14 required reason → poll 13 → Approved navigation ID → 09/10/11 | 422 blank reason; 409 duplicate; stale token/ended treatment 404; Rejected/Revoked/Expired terminal CTA disabled. |
| E global owner inbox | 15 badge → 16 list → 17 detail → 18/19 → 20 → 21 | Backend capabilities; no practice switching; fresh version refetch after 409; late approval 409 Expired. |
| F owner visibility | EXT-03 list → EXT-02 detail; EXT-01 chosen Hidden completion; 22 toggle → 23 history | Atomic completion; stale version 409; completed owner/practice only; subsequent shared reads reauthorize. |
| G dependent | Existing families listing/capabilities → 24 → 25–29 | Current approved policy, exact verified adult-child proof and age under 16 required. Father/Mother need no separate medical eligibility; Guardian/LegalGuardian require current eligibility and authority proof. Effective restriction, proof/Guardian eligibility revocation or birthday blocks new reads/downloads. Restoration only removes restriction. |

No foreign drill-down uses an original owner-only Rx/Lab/Radiology API. After
mutations, current status/capabilities/rowversion are returned without an extra
lookup. Polling is bounded; no SignalR or new email dependency is required.

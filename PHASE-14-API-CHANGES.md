# Phase 14 API changes — 2026-10-05

All routes below use `/api/v1`, bearer authentication and existing ProblemDetails semantics. Enum values serialize as strings. Paged responses use the existing ClinicalPage shape (`items`, `totalCount`, `pageNumber`, `pageSize`). Default page size is 20, maximum 100. Swagger specifies request, response and multipart schemas.

## New routes (44)

| Actor | Method | Route after `/api/v1` | Result |
| --- | --- | --- | --- |
| Root | GET | `/admin/drug-catalog-managers` | Paged manager accounts; search/pageNumber/pageSize |
| Root | GET | `/admin/drug-catalog-managers/{id}` | Manager account |
| Root | POST | `/admin/drug-catalog-managers` | 201 manager account |
| Root | PUT | `/admin/drug-catalog-managers/{id}` | Updated email/phone |
| Root | POST | `/admin/drug-catalog-managers/{id}/activate` | Active account |
| Root | POST | `/admin/drug-catalog-managers/{id}/deactivate` | Inactive account |
| Doctor | GET | `/doctors/me/drug-catalog` | Active SQL search; search/pageNumber/pageSize |
| Manager | GET | `/admin/drug-catalog` | All-status catalog; search/status/pageNumber/pageSize |
| Manager | GET | `/admin/drug-catalog/{drugId}` | Drug and immutable history |
| Manager | POST | `/admin/drug-catalog` | 201 drug and history |
| Manager | PUT | `/admin/drug-catalog/{drugId}` | Updated drug and history |
| Manager | POST | `/admin/drug-catalog/{drugId}/activate` | Activated drug and history |
| Manager | POST | `/admin/drug-catalog/{drugId}/deactivate` | Inactive drug and history |
| Manager | POST | `/admin/drug-catalog/{sourceDrugId}/merge` | Merged source and history |
| Manager | POST | `/admin/drug-catalog/imports/preview` | Persisted Staged batch/counts |
| Manager | GET | `/admin/drug-catalog/imports` | Paged import batches |
| Manager | GET | `/admin/drug-catalog/imports/{batchId}` | Batch metadata/counts/token |
| Manager | GET | `/admin/drug-catalog/imports/{batchId}/changes` | Paged source/change rows; optional changeType |
| Manager | POST | `/admin/drug-catalog/imports/{batchId}/apply` | Applied batch/counts |
| Doctor | GET | `/doctors/me/drug-catalog-requests` | Paged own requests; optional status |
| Doctor | GET | `/doctors/me/drug-catalog-requests/{requestId}` | Own request/history/canonical drug |
| Doctor | POST | `/doctors/me/drug-catalog-requests` | 201 name-only-or-richer request |
| Doctor | PUT | `/doctors/me/drug-catalog-requests/{requestId}` | Updated/resubmitted own request |
| Manager | GET | `/admin/drug-catalog-requests` | Paged requests; status/doctorId/search |
| Manager | GET | `/admin/drug-catalog-requests/{requestId}` | Medication-only request/history |
| Manager | POST | `/admin/drug-catalog-requests/{requestId}/request-more-info` | NeedsMoreInfo request |
| Manager | POST | `/admin/drug-catalog-requests/{requestId}/approve` | Approved request/canonical drug |
| Manager | POST | `/admin/drug-catalog-requests/{requestId}/reject` | Rejected request/optional duplicate drug |
| Doctor | POST | `/doctors/me/practices/{practiceId}/encounters/{encounterId}/prescription/items` | Add item; implicit initial root; full state |
| Doctor | PUT | `/doctors/me/practices/{practiceId}/encounters/{encounterId}/prescription/items/{itemId}` | Updated initial draft/full state |
| Doctor | DELETE | `/doctors/me/practices/{practiceId}/encounters/{encounterId}/prescription/items/{itemId}` | Removed initial item/full state |
| Doctor | POST | `/doctors/me/prescriptions/{prescriptionId}/correction-draft` | Start/resume correction/full state |
| Doctor | GET | `/doctors/me/prescriptions/{prescriptionId}/correction-draft` | Correction/full state |
| Doctor | POST | `/doctors/me/prescriptions/{prescriptionId}/correction-draft/items` | Add correction item/full state |
| Doctor | PUT | `/doctors/me/prescriptions/{prescriptionId}/correction-draft/items/{itemId}` | Update correction/full state |
| Doctor | DELETE | `/doctors/me/prescriptions/{prescriptionId}/correction-draft/items/{itemId}` | Remove correction item/full state |
| Doctor | POST | `/doctors/me/prescriptions/{prescriptionId}/correction-draft/finalize` | New Finalized/prior Superseded/full state |
| Doctor | POST | `/doctors/me/prescriptions/{prescriptionId}/correction-draft/discard` | Removed draft/prior current/full state |
| Doctor | POST | `/doctors/me/prescriptions/{prescriptionId}/void` | Current Voided/full state |
| Doctor | GET | `/doctors/me/prescriptions/{prescriptionId}` | Own current/draft state |
| Doctor | GET | `/doctors/me/prescriptions/{prescriptionId}/versions` | Own version array |
| Doctor | GET | `/doctors/me/prescriptions/{prescriptionId}/versions/{versionNumber}` | Own single version |
| Patient | GET | `/prescriptions/mine` | Paged own completed current summaries |
| Patient | GET | `/prescriptions/mine/{prescriptionId}` | Own completed patient-facing current output |

`Manager` means DrugCatalogManager, with the corresponding current permission. Root's account authority grants no catalog or prescription route override. There is no catalog DELETE, explicit initial Prescription POST, active-visit finalize, arbitrary patient-ID endpoint or Reception prescription endpoint.

## Account/catalog/request bodies

Root account creation accepts `userName`, `email`, nullable `phoneNumber`, `initialPassword`, `confirmPassword`. New accounts require the existing first-login password-change endpoint before catalog operations. Account PUT accepts email/phoneNumber. Activate/deactivate require no body.

Catalog POST accepts DrugData: `commercialNameEn` plus optional `commercialNameAr`, `scientificName`, `manufacturer`, `drugClass`, `route`, `strengthText`, `dosageForm`, `priceEgp`. PUT uses `{ "data": { ...DrugData }, "rowVersion": "...", "reason": "..." }`. Activate/deactivate use `{ "rowVersion": "...", "reason": "..." }`; merge also requires `targetDrugCatalogId`. Prices are reference-only. Doctor search omits internalNotes and exposes suggestions separately from verified strength/form.

Request POST accepts `medicationName` (required), optional `scientificName`, `manufacturer`, `strength`, `dosageForm`, `route`, `drugClass`, `doctorNote`. Request PUT uses `{ "data": { ...MedicationRequestData }, "rowVersion": "..." }`. Review actions accept `rowVersion`, reason (required for more-info/nonduplicate reject), required `approvedData` for approval or `duplicateOfDrugCatalogId` for duplicate rejection. Approval requires manager-supplied normalized approvedData and creates an Active canonical row in the same transaction. The response contains approvedDrugCatalogId/duplicateOfDrugCatalogId and corresponding `approvedDrug`/`duplicateDrug` display objects. No patient/encounter fields are accepted.

Preview is `multipart/form-data` with `file` (JSON, max 20 MiB), optional `sourceVersion`, optional `sourceCommitSha`. Apply takes only the batch ID and Idempotency-Key, with no file upload. The immutable batch retains server-computed fileSha256, classifications/counts, source metadata and timestamps. Source JSON uses the seven MedicianDB snake_case fields; it has no trusted Wasla ID, strength or dosage form.

## One-call add and draft edits

Catalog add uses the search result directly; a second catalog details request is unnecessary:

```json
{
  "drugCatalogId": "<active-drug-id>",
  "prescriptionRowVersion": "<root-token-if-root-exists>",
  "strength": "400 mg",
  "dosageForm": "Tablet",
  "route": "Oral",
  "dose": "1 tablet",
  "frequencyCode": "TwiceDaily",
  "durationType": "Fixed",
  "durationValue": 7,
  "durationUnit": "Days",
  "quantityValue": 14,
  "quantityUnit": "Tablet",
  "instructions": "After food"
}
```

Missing medicine uses the same endpoint with `newMedication` instead of drugCatalogId:

```json
{
  "newMedication": { "medicationName": "Medication absent from catalog" },
  "prescriptionRowVersion": "<root-token-if-root-exists>"
}
```

The second example creates both the review request and incomplete DoctorSubmitted draft item atomically. Fill the clinical fields through PUT before finalization; manager review is independent. Supply exactly one source on add. Omit the token only when no prescription root exists. Correction add/edit accepts the same fields; `rowVersion` is an alias for `prescriptionRowVersion`. PUT replaces the clinical fields and cannot change medication/source identity. It accepts partial clinical completeness, rather than JSON Merge Patch behavior.

Optional PRN fields are `asNeeded`, `prnReason`, `minimumIntervalText`, `maxPer24HoursText`. Non-PRN requires a frequency code; Custom also requires frequencyText. Ongoing duration uses `durationType: "Ongoing"` and null value/unit. Quantity value/unit must be both absent or both valid.

Both DELETE item routes take `?rowVersion=<URL-encoded-root-token>` and no body. Removing the last initial item returns null prescriptionId/current/draft/rowVersion; removing the last correction item keeps an empty draft until discard.

## Full prescription response and existing routes

All prescription mutations return `PrescriptionStateResponse`:

```text
prescriptionId, medicalEncounterId, practiceId, rowVersion
current: nullable version
draft: nullable version
capabilities: canManageDraft, canRequestNewMedication, canStartCorrection,
              canFinalizeCorrection, canDiscardCorrection, canVoid
completionBlockers: [{ code, itemId, field }]
```

Versions include versionId/versionNumber/status/previousVersionId, lifecycle times, owning-Doctor correction/void reasons and ordered items. Each Doctor item includes itemId/sortOrder/source IDs, medicationName/scientificName and a nested `clinical` object containing the item inputs. Search suggestions remain separate from verified input. Patient item DTOs contain only sortOrder/name/scientificName/clinical; patient details also include Doctor/Practice identity, visitDateUtc, version number/status and finalized/voided times. Patients do not receive draft/history/source IDs/managerial reasons or prices.

Existing Doctor Encounter GET by ID/by Ticket now includes `prescription`, prescription/clinical completionBlockers and `canManagePrescription`/`canRequestNewMedication` capabilities. A missing prescription is null. Existing Ticket details include nullable prescriptionId. Phase 13 patient encounter DTOs are unchanged.

Start Visit is unchanged. Complete Visit remains `POST /practices/{practiceId}/tickets/{ticketId}/complete`, now accepting:

```json
{
  "ticketRowVersion": "<ticket-token>",
  "encounterRowVersion": "<encounter-token>",
  "prescriptionRowVersion": "<prescription-root-token-if-draft-exists>"
}
```

All blockers are reported before committing, and prescription finalization shares the existing completion/follow-up transaction. No prescription permits omitting its token. A missing/stale token for an existing draft conflicts. Clinical notes and Phase 13 diagnosis rules remain required as before. Completing an invalid prescription returns 422 with item-specific Error.Source (`prescription.items/{itemId}/{field}`) and all remaining errors.

Correction start requires `{ "rowVersion": "...", "reason": "..." }`. Starting with an existing draft resumes it. Finalize/discard require `{ "rowVersion": "..." }`; void also requires reason. Patients keep seeing the prior finalized output while a correction is open and switch only after successful finalize. Void is blocked while a draft exists and remains clearly visible as Voided.

## Retries and frontend behavior

Use a stable `Idempotency-Key` for merge, import apply, request approve, every add-item (catalog or Request + Add), correction start/finalize and void. Complete Visit retains its existing idempotency header. Repeating the same actor/operation/key and payload creates no duplicate rows; changing the payload returns 409. Prescription retry responses contain the latest authorized state/token, including a draft removed by a later action, rather than recreating it.

Replace local state with each mutation response. Retain and send the root token, render capabilities/blockers, and refetch on 409. A catalog deactivation/merge race returns DrugCatalog.NotActive; refresh search or use missing-medication submission. Never wait for request review to finish an active visit. Never infer final clinical strength/form from search suggestions without Doctor input. Preserve existing rowversions for Ticket/Encounter independently of the prescription root.

Errors use localized en/ar messages: 422 for input/finalization validation, 409 for state/token/key conflicts, 403 for actor/permission denial, and safe 404 for foreign/unavailable clinical data. The new error codes and response schemas are covered by Phase14ContractTests.

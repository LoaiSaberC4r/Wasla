# Wasla Phase 14 implementation addendum — 2026-10-05

Branch: `codex/phase14-drug-catalog-prescriptions`, based on `main` at `5ec70b6`.

This addendum records the approved Phase 14 task. It supersedes only the prescription/catalog deferral in Phase 13 DEC-069. All Phase 13 ownership, notes, diagnosis, amendment, follow-up, Start Visit and atomic Complete Visit rules remain in force.

## Accounts and permissions

`DrugCatalogManager` is a platform account type and deterministic system role (`10000000-0000-0000-0000-000000000005`). It uses ApplicationUser/UserRole and the existing first-login password change flow; it has no Doctor, Patient or practice profile. Only the Root SuperAdmin can list, read, create, update, activate or deactivate these accounts. Ordinary SuperAdmins cannot govern them or change the manager role. The manager role can contain only its ten catalog permissions, including when Root changes permissions.

The manager defaults are `DrugCatalog.View`, `Create`, `Update`, `Activate`, `Deactivate`, `Merge`, `Import`, `ImportHistory` under the same prefix, and `DrugCatalogRequests.View` / `Review`. It receives no clinical, patient, encounter, prescription, reservation, queue or finance permission. Catalog requests contain medication information and a submitting Doctor identifier, never a patient/encounter association.

Doctors receive `DrugCatalog.SearchActive`, `DrugCatalogRequests.CreateOwn`, `ViewOwn`, `UpdateOwn`, and `Prescriptions.ViewOwn`, `ManageOwnDraft`, `CorrectOwn`, `VoidOwn`. Patients receive `Prescriptions.ViewOwnCompleted`. Root account governance adds the six `DrugCatalogManagers.*` permissions. The 25 permission names are appended without changing prior deterministic IDs. Reception and SuperAdmin receive no prescription access by default, and role/type checks prevent using these routes as a clinical override.

Every medication operation checks authentication, active account, completed first login, account type, claimed permission and current database permissions. Doctors must be approved and own the active practice. Patients resolve their profile through their authenticated account link. IDs belonging to another Doctor/Patient return the existing safe scope result. Family booking authority does not grant clinical viewing.

## Catalog and offline import

Wasla SQL Server is the runtime catalog. `MedicianDB/data/egyptian-drugs.json` is an external source for a controlled, authenticated multipart upload. There is no runtime GitHub call, startup fetch, online synchronization or medicine seeding. Source fields are commercial English/Arabic names, scientific name, manufacturer, class, route and EGP reference price; source strength/form are absent and remain nullable.

Statuses are `Active`, `NeedsReview`, `Inactive`, `Merged`. Initial CANCELLED/ILLEGAL markers produce Inactive with a reason. Missing scientific name produces NeedsReview. UNKNOWN alone does not deactivate a row; a populated scientific name otherwise permits Active. Cosmetic, toothpaste, shampoo, cream, supplement or non-medicine heuristics do not classify records. Original Arabic/English strings remain available; separate normalized search fields handle case, whitespace and Arabic variations.

Manager create/update, activation, deactivation and explicit merge record actor, time, action, reason and immutable before/after snapshots. No catalog hard-delete endpoint exists. Merge requires a distinct Active target and reason, makes the source Merged and links it to the target. It never rewrites historical prescription references or snapshots, never automatically redirects a stale selection, and does not permit reuse of a merged source.

Preview validates the complete seven-field JSON schema, size (20 MiB), maximum row count (100,000), lengths and price values, computes the file SHA-256 on the server, and persists a Staged batch and all source rows. Optional source version/commit metadata is retained. Staging records identify new, unchanged, price-only, NeedsReview, missing, possible duplicate and exact duplicate rows; batch counts and paged changes are reviewable before applying.

Business catalog IDs are generated independently of source identity. Source identity fingerprints exclude price; content hashes include it. Exact repeats are reported and skipped. A second price variant of the same identity stays staged for review. New clinical identities with an existing name or matching scientific/manufacturer candidate do not overwrite existing data: they become separate NeedsReview candidates. There is no automatic merge. Existing manually reviewed clinical values and manually overridden prices are preserved. Eligible price-only changes update reference prices with history. Missing imported rows receive source-presence metadata; they are neither deleted nor automatically deactivated.

Apply runs transactionally against the persisted preview. Changed matched catalog rowversions or competing source identities require a fresh preview. Imports are serialized, source fingerprints are unique, and repeated application creates no second catalog set. Upload parsing/materialization is confined to controlled imports. Runtime Doctor search is SQL-filtered, ranked and paged; exact/prefix name matches precede broader scientific/manufacturer matches. Page size is capped at 100, with stable ordering and total counts.

Search returns enough medication metadata to add directly by DrugCatalogId. Only Active records are returned to Doctors. Strength/form parsing yields suggestions only; no parser suggestion becomes a verified catalog value or a final prescription field automatically. An UNKNOWN/unusable route is exposed to Doctors as missing. Add rechecks current Active status under the catalog resource lock and returns `DrugCatalog.NotActive` (409) for a deactivated/merged selection.

Import rollback is transactional on failure. Reversing a successful import requires reviewed catalog changes with history; it does not delete data or rewrite prescriptions. A later import never silently restores an intentionally inactive/merged status.

## Missing medication requests

A Doctor can submit a name-only request. Its states are Pending, NeedsMoreInfo, Approved and Rejected. The owning Doctor may edit Pending/NeedsMoreInfo; updating NeedsMoreInfo resubmits to Pending and appends history. Approved/Rejected requests are terminal. Managers can request information with a reason, approve into a canonical catalog row, or reject with a reason; duplicate rejection links a canonical DrugCatalogId. Responses include that canonical medication's display data.

During an active consultation or correction, the add-item endpoint accepts either DrugCatalogId or `newMedication`. The latter creates the request and a DoctorSubmitted item atomically in one call. It requires only a medication name; review is never a prerequisite for completing a visit. Later approval, rejection, catalog updates and merging never convert or rewrite the submitted prescription snapshot.

## Prescription lifecycle and clinical rules

Prescription is a separate aggregate: one root per MedicalEncounter, one draft maximum, one current Finalized version maximum, and a linear version chain. DoctorId and PatientId derive from the trusted encounter; practice derives through that encounter. The root owns concurrency. Version numbers are monotonic, including after discard, and items have fresh IDs when cloned for correction. Ticket stores no prescription items.

Start Visit keeps its existing behavior. First item addition implicitly creates the initial Draft; there is no explicit create-prescription or initial-finalize endpoint. Drafts permit incomplete fields, while rejecting invalid supplied values. Removing the last initial item removes the empty draft/root, returns null prescription/token state and leaves the visit able to complete without a prescription. Empty correction drafts remain until explicit discard or valid finalization.

Each item has exactly one source: DrugCatalog with catalog reference, or DoctorSubmitted with request reference. Medication name and scientific name are snapshots; strength, form and route are editable clinical snapshots. Verified catalog values may autofill missing input. A final item requires strength, dosage form, usable route, dose, duration and frequency when not PRN. Custom frequency requires text. Fixed duration requires a positive integer and Days/Weeks/Months; Ongoing prohibits value/unit. PRN requires reason and minimum interval; maximum per 24 hours is optional. Optional quantity must be positive and have a unit. Instructions are optional. Quantity has no dispensing/inventory behavior. No price, internal catalog note or managerial review note enters a clinical/patient item snapshot.

Complete Visit accepts Ticket, Encounter and, when a draft exists, Prescription root rowversions. It collects all notes/diagnosis/prescription blockers before mutation, then atomically finalizes the initial prescription, completes Ticket and Encounter, and consumes follow-up eligibility through the existing transaction. Failure rolls all of them back, including audit/idempotency writes. Completion without a prescription remains valid. Validation errors identify `prescription.items/{itemId}/{field}`; state and encounter responses expose structured completion blockers and server-computed capabilities.

After completion, a reasoned correction starts by cloning the current Finalized version. Starting again while a correction exists resumes the same draft. The prior finalized version remains patient-visible while the draft is edited. Finalizing a valid, nonempty correction atomically supersedes the prior version and makes the new version Finalized. A failed finalize leaves both prior Finalized and Draft states unchanged. Discard deletes only the correction draft/items, appends audit and returns the prior current version. Finalized/Superseded/Voided items and completed snapshots cannot be edited or deleted through ordinary persistence/API paths.

Void requires a reason, a current Finalized version, no correction draft and a current root token. It preserves the version/items and marks it Voided. Superseded versions remain immutable history. The patient sees a clear Voided status; no correction or recreation is permitted after void in this phase.

## Visibility, persistence and concurrency

Doctor APIs expose only the owning Doctor's prescriptions, versions and correction state. Patient APIs expose only their own completed encounter's current Finalized/Voided output. Drafts and superseded versions, request/source IDs, audit, internal review/correction/void reasons and catalog prices are omitted from patient DTOs. Patient list queries project current summaries without loading historical version collections.

Rowversions protect catalog, request, import and prescription roots. Prescription item mutations advance the root even though the item has no independent token. SQL application locks coordinate initial creation, completion, correction, void, catalog selection/merge and import. SQL unique/check constraints protect root-per-encounter, version numbering, draft/current uniqueness, linear predecessor, exclusive source references, duration and quantity. Restrict foreign keys preserve retained medical/catalog data; client-side draft cleanup permits the explicitly approved empty/discard lifecycle.

Append-only PrescriptionAuditEvents retain created/finalized/correction/discard/void action, actor and time metadata. Retained versions hold correction/void reasons. Their root/version IDs are retained identifiers rather than foreign keys so deleting an approved transient draft/root does not destroy or prevent its audit. Prescription/history storage does not use soft delete. Persistence rejects changes/deletions to protected finalized and immutable history records. Superseding the prior current row is ordered before inserting the new current state in the same transaction to satisfy the unique filtered index.

`Idempotency-Key` is required for catalog merge, import apply, request approval, prescription add (including Request + Add), correction start/finalize and void. Records are scoped to actor and operation. Matching retries perform no duplicate mutation; differing payloads return 409. Prescription retries return the latest full state and token, including null state after an initial draft was removed, after rechecking scope. Failed transactions retain no success key. Complete Visit preserves existing Ticket idempotency and includes the prescription token in its fingerprint.

Only protected database snapshots/response records contain clinical payloads. Logs redact medication request/draft/item data, dose, PRN details, instructions, reasons and before/after/response JSON. Audit logs retain identifiers/action/time. Phase 14 adds no notification/outbox messages. English and Arabic errors are present; validation is 422, stale/state conflicts 409, denied role 403 and unavailable foreign resources use safe 404 semantics.

## API and deployment

The 44 new routes and changed Encounter/Ticket contracts are documented in `PHASE-14-API-CHANGES.md` and reflected in Swagger. Mutations return full current/draft state, root token, capabilities and blockers. Frontends should replace their local state with each response and refetch after a conflict. Apply the additive migration, run the deterministic security seeder, create a manager through Root governance, complete first-login password change, then preview/review/apply an offline dataset. Production deployment and catalog import are separate operational steps; no production database was changed during this implementation.

Deferred: pharmacy/dispensing/stock, insurance, interactions/clinical decision support, automatic medication synchronization, e-prescription transmission, laboratory/radiology orders, cross-doctor medical-record sharing, family/guardian clinical access, broader medical-record policy and medication notifications. Reference price is descriptive catalog information only, never prescription pricing, fees, payments or revenue.

Verification and exact counts are in `PHASE-14-CLOSURE-REPORT.md`; affected baseline areas are in `PHASE-14-IMPACT-PLAN.md`.

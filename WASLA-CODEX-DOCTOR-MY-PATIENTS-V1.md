# CODEX IMPLEMENTATION TASK — WASLA / DOCTOR MY PATIENTS V1

**Project:** Wasla Healthcare Platform  
**Target:** .NET 10 / EF Core / SQL Server / Clean Architecture / CQRS  
**Task type:** Backend implementation + security hardening + API contracts + tests + source-of-truth addendum  
**Priority:** Business correctness and medical privacy first.  
**Scope:** Doctor My Patients Workspace ONLY. Do not refactor unrelated modules.

## 0. MISSION AND SUCCESS CRITERIA

Implement a Doctor-wide `My Patients` workspace that lists UNIQUE platform-global Patients who actually attended this Doctor's Practice(s); provides read-only patient overview, own attendance and clinical history, own prescription history with safe PDF export, past/upcoming appointments and follow-up history; reuses the existing Lab/Radiology and Follow-Up workflows; and allows a carefully scoped, **read-only historical access path** for the Doctor's OWN clinical records at inactive/closed Practices. Preserve the Phase 16 active-treatment-only cross-doctor visibility gate. Do not introduce Patient ownership by Doctor.

The resulting backend must support these UI flows: Directory -> Patient Overview -> Attendances/Ticket History -> Encounter Details -> Diagnosis Timeline -> Prescriptions/Versions/PDF -> Lab/Radiology/Submission Review -> Appointments -> Follow-Up -> (when currently treating only) Shared Medical Record. All API references used by the UI must have an explicit scope/capability and respond safely to revoked permissions.

**No `DoctorPatient` business aggregate/table by default.** This is a read-model and relationship-policy feature, not a second mutable Patient profile. No new Doctor direct-booking command, clinical mutation, patient identity registration, or cross-doctor sharing policy is authorized by this task.

## 1. REQUIRED SOURCE REVIEW / CHANGE CONTROL — BEFORE CODING

1. Read the actual available Master Source of Truth, including `Healthcare-Platform-Master-Source-of-Truth-v1.2-Updated.txt` if supplied to the workspace. The Master is authoritative. The source was provided to the product-design conversation but might not physically exist in the GitHub checkout: **never claim it was read or updated if absent**.
2. Read repository addenda and code for Phases 5, 10–18, particularly `SOURCE-OF-TRUTH-PHASE-13-ADDENDUM.md`, `SOURCE-OF-TRUTH-PHASE-14-ADDENDUM.md`, `SOURCE-OF-TRUTH-PHASE-15-ADDENDUM.md`, `SOURCE-OF-TRUTH-PHASE-16-FINAL-BUSINESS-ADDENDUM.md`, and the Phase 18 Archive Part Two documents. Preserve later approved decisions that supersede earlier ones.
3. Inspect BuildingBlock patterns in the actual solution: Result/Error, CQRS/MediatR, FluentValidation, Specifications, read persistence, `ICurrentUser`, current DB access snapshot, permission authorization, audit, ProblemDetails, concurrency, controlled migrations, rate limiting and no-store clinical responses.
4. Inspect the concrete modules and tests listed in section 7. QControl is conceptual background only; do not copy its domain or infrastructure.
5. Publish a small business-rule-to-code mapping and proposed file list before implementation. Label `CONFIRMED`, `APPROVED IN THIS TASK`, `PROPOSED TECHNICAL DESIGN`, `DEFERRED`, and any genuine blockers. Changes to an old approved rule require explicit change control; do not silently remove security conditions.
6. Record all owner-approved My Patients decisions in an additive `SOURCE-OF-TRUTH-DOCTOR-MY-PATIENTS-V1-ADDENDUM.md` (or the repository's current accepted addendum convention), and update relevant API/implementation docs. Do NOT invent a legal sign-off or claim formal Master adoption where this has not happened.

## 2. OWNER-APPROVED BUSINESS RULES (MANDATORY)

**MP-BR-01 — Global identity and membership.** Patient is platform-global and remains the same `PatientId` regardless of Doctor/Practice. My Patients is a Doctor-scoped relationship view, NOT ownership. A patient enters the Doctor's directory at the first **successful actual check-in** creating a Ticket from a Reservation, or successful Direct Walk-In Ticket. Reservation-only, receptionist registration-only and no-show bookings without Ticket do not qualify. Ticket `Waiting`, `Called`, `InProgress`, `Completed`, `Cancelled`, or `NoShow` remain evidence of a recorded attendance once the valid Ticket was created; these statuses are NOT proof a clinical examination occurred. Use the existing trusted ticket check-in/provenance model; do not manufacture attendance from other records.

**MP-BR-02 — One patient per Doctor.** Deduplicate by `(DoctorId, PatientId)` across all DoctorPractices, including inactive/closed Practices and archived Tickets. A patient may independently appear in several Doctors' lists. A Basic Patient with no linked login may appear. Patient account closure does not erase their historical clinical/attendance record.

**MP-BR-03 — Patient data / contact.** An Approved, active Doctor with current effective permission and actual attendance relationship can view the patient's current `NameAr`, optional `NameEn`, `DateOfBirth`, `Age` (calculated), `Gender`, `PhoneNumber` and `Email` in My Patients without a separate contact-specific permission. Nullable contact data remains null (especially minors). Do NOT auto-return guardian/family contacts, identity evidence/documents, `PatientContact` family details, global record content or media storage keys. Doctor may NOT change the platform Patient profile through My Patients. Patient/Reception profile editing stays in its existing verified workflow.

**MP-BR-04 — Visit history.** Show every actual attendance across all the Doctor's Practices with immutable operational Ticket history and linked MedicalEncounter, if any; include archived tickets. Explicitly distinguish `Ticket` from `MedicalEncounter`. Do not backfill fabricated clinical Encounters for old tickets. Reservation-only bookings never count as attendance.

**MP-BR-05 — Doctor's own clinical history.** Show Doctor's own currently active Diagnoses as a Doctor-wide chronological timeline; complete Clinical Notes only within authorized own Encounter Details; immutable Amendment History via existing workflow. InProgress edits use the existing commands and capabilities. Completed clinical records may only be corrected by existing Amendment workflow. Preserve voided/removed records in immutable history, not as active diagnoses. No mutation is added by this feature.

**MP-BR-06 — Doctor's prescriptions.** List Doctor's own previously issued prescriptions for this patient, including versions and `Finalized`, `Superseded`, `Voided` status; drafts remain solely within their proper active Encounter context and must never be exported as an issued Rx. Reuse existing read/version/correction/void commands (no direct finalized edit). Provide secure `View`, `Print`, `Download PDF` for authorized **issued versions**; clearly mark superseded or voided documents as **NOT CURRENT / NOT VALID FOR CURRENT TREATMENT**. Do NOT treat PDF generation as legally certified e-prescribing or add digital signing; legal certification is deferred.

**MP-BR-07 — Cross-Doctor sharing is unchanged.** The Doctor can always read their OWN records under own-record authorization. Other Doctors' completed, patient-safe `Visible` prescriptions/diagnoses/official lab/radiology outputs are accessible ONLY with active current treating Ticket AND active InProgress Encounter of the identical patient, valid Doctor/Practice scope, current `MedicalRecords.ViewTreatingPatient` permission and actually enabled Phase 16 sharing configuration (`MedicalRecord:CrossDoctorSharingEnabled` / `MedicalRecord__CrossDoctorSharingEnabled`), plus existing rechecks/audit. `Hidden` remains a date-only placeholder; use existing owner-request/approval workflow, scoped to requester/current ticket/source Encounter/visibility revision. No private notes, drafts, pending submissions or arbitrary other-Doctor versions. Ending the current Encounter ends cross-doctor entitlement. No unscoped query, prefetch/caching, PDF export or new patient route can bypass this. **Production/Staging sharing flags must NOT be enabled by this task.**

**MP-BR-08 — Lab/Radiology.** Within own Doctor scope, provide own Lab/Radiology requests, accepted official results, attachment/version histories, and patient-submitted `PendingReview` uploads on the Doctor's own requests. Link Review CTA to existing scoped Doctor Review endpoints. Only the Doctor owning the original request can Accept/Reject according to the Phase 15 workflow. Pending submissions are NOT official results or shared cross-doctor medical outputs. Reuse existing doctor endpoints filtered by `patientId` whenever feasible; avoid redundant wrappers.

**MP-BR-09 — Appointments / Follow-Up.** Read all Doctor-scoped patient Reservations across his Practices, both historical and upcoming, including standalone bookings and archived history; attending is not required for a Reservation to appear in this tab *once the patient is already a My Patients member*. Doctor may create a `FollowUpEligibility` only from their own eligible **Completed** Encounter, with `ValidUntil`, through the current command. The Patient or assigned Reception performs the actual FollowUp booking through the current availability/segment/VisitType/pricing/reservation workflow. Do NOT add Doctor Direct Booking, separate Booking Request Inbox, new notification rules or synthetic eligibility. One eligibility = one FollowUp; subsequent FollowUp needs an eligibility from a later completed Encounter.

**MP-BR-10 — Closed Practice historical read.** Patients of a Doctor's inactive/closed Practice remain in the directory. A Doctor whose account is **active**, first login completed, and professional Doctor status **Approved** may read **their OWN historical** attendance/Encounter/diagnosis/prescription/lab/radiology/reservation records from that inactive Practice, only with explicit `DoctorPatients.ViewHistoricalOwn` AND the relevant source's original read permission. This is an **additional read-only authorization path**; not a blanket exemption from `Practice.IsActive`. Never grant booking, Check-In, Start/Complete Encounter, Notes/Diagnosis Amendment, Rx Correction/Void, Lab/Radiology upload/review/correction, Follow-Up creation, cancellations/reschedules, payment/financial mutations, or other writes on an inactive Practice. Mutating capabilities must report false and handlers must fail closed. No new access to data created by other Doctors. A Suspended/Rejected/unapproved Doctor or inactive account receives no My Patients access, including historical.

**MP-BR-11 — Directory UX defaults (delegated technical/product selection).** All-time list by default, newest check-in descending, deterministic tie-breaker `PatientId`. Search by Arabic/English name, normalized phone, and email strictly **within this Doctor's already eligible patients**. Filters: owned Practice, last attendance date range, minimum attendance count, upcoming appointment, active vs historical-only, pagination. Show metadata such as first and last attendance, total attendance count, completed Encounter count (only with corresponding clinical permission), number of Doctor Practices attended, last Practice and existence of upcoming Reservation (only with corresponding reservation permission). `Returning` means >=2 attendances to this Doctor. Provide useful all-time total/last-30-days/returning statistics, clearly distinguishing all-time vs date-filtered counts. No misleading counts of global Patients or unrelated Doctors' outputs.

**MP-BR-12 — Security / privacy.** DoctorId always derived from the currently authenticated DB-validated Doctor, never trusted from URL/body. `patientId` never acts as authorization by itself. Any DoctorPatients read must first prove `(DoctorId, PatientId)` attendance relationship. Resource-specific clinical and financial rights remain separate. Return safe `404` for unrelated patient/resource IDs, fail closed on revoked permissions, prohibit sensitive request/response logging and shared HTTP caches, audit medical reads/export/downloads. Do not bypass Family/Guardian/Minor policies, Identity/Claim verification, audit, retention or medical records gates.

## 3. PROPOSED API CONTRACT — EIGHT NEW GET ROUTES (MP-01..08)

**Base route**: `/api/v1/doctors/me`. All methods require authentication, active + Approved Doctor and DB-validated permissions. Use versioned controllers, ProblemDetails, validation, `Cache-Control: no-store`, `X-Content-Type-Options: nosniff` on sensitive responses, and pagination guards. Route/DTO design is the implementation target; if exact existing shape conflicts, adapt explicitly in contract documentation rather than silently altering old endpoints.

### MP-01 — Directory + summary
`GET /doctors/me/patients?search=&practiceId=&lastAttendanceFrom=&lastAttendanceTo=&minAttendanceCount=&hasUpcomingAppointment=&practiceScope=all|active|historicalOnly&pageNumber=1&pageSize=20`

Response `DoctorPatientsPageResponse { items: DoctorPatientListItem[], totalCount, pageNumber, pageSize, summary: { allTimeUniquePatients, attendedLast30Days, returningPatients } }`. Each item: `{ patientId, nameAr, nameEn?, phoneNumber?, email?, dateOfBirth, age, gender, firstAttendanceAtUtc, lastAttendanceAtUtc, lastAttendanceBusinessDate, attendanceCount, completedEncounterCount?, practiceCount, lastPractice:{id,nameAr,nameEn?,isActive}, hasUpcomingAppointment?, historicalOnly, capabilities }`.

- `completedEncounterCount?` and `hasUpcomingAppointment?` are **permission-filtered**; do not leak medically protected counts when the source permission is missing. Express null/unavailable rather than misleading 0.
- Filter by `practiceId` only after asserting it belongs to current Doctor. Global membership remains Doctor-wide; Practice filter narrows qualifying attendance rows and their statistics by documented semantics, never changes the global Patient identity.
- `lastAttendanceFrom/To` refer to last attendance in the scoped result; document business-date timezone semantics. `Last30Days` computed at server time and with an explicit definition.
- Stable ordering, bounded search, bounded page size (default 20, max 100), SQL-side aggregation, no client-side dedupe, no in-memory all-ticket scan, no N+1.
- Search may return unmasked approved contact data only for already membership-qualified patients. No global search endpoint and no guardian contact fallback.

### MP-02 — One Patient Overview
`GET /doctors/me/patients/{patientId}`

Response `DoctorMyPatientOverviewResponse`: read-only patient identity/contact, own attendance summary, last attendance/Ticket+Encounter references (when readable), last own diagnosis/prescription summary and request counters **only if source permissions are present**, next Doctor-scoped appointment if authorized, current own FollowUp status, `canView...` capability flags, `historicalOnly` marker, owned Practice names. Never include other Doctor's medical record, internal clinical notes in overview, family details or a mutable Patient rowVersion action. Relationship check first; 404 for unrelated IDs.

### MP-03 — Unified Attendance History
`GET /doctors/me/patients/{patientId}/attendances?practiceId=&status=&fromDate=&toDate=&pageNumber=1&pageSize=20`

Response `DoctorPatientAttendancePage`: unique Ticket rows for all qualifying live+archived Tickets, sorted newest with stable key. Fields: `ticketId`, `practiceId`, `practiceName`, `businessDate`, `checkInAtUtc`, `source`, `checkInMode`, `ticketStatus`, `ticketNumber`, `isArchived`, `linkedReservationId?`, `encounterId?`, `encounterStatus?`, safe presentation flags/links. Never imply a Ticket = completed clinical Encounter. Link to existing ticket/encounter read only if each existing endpoint can authorize it; historical closed Practice read path must be handled safely. Ticket history detail uses existing audited operational endpoint (do not duplicate mutation capabilities).

### MP-04 — Own Diagnosis Timeline
`GET /doctors/me/patients/{patientId}/diagnoses?practiceId=&fromDate=&toDate=&pageNumber=1&pageSize=20`

Return chronological items `{diagnosisId, encounterId, practiceId, businessDate/encounterDate, diagnosisType, displayText, encounterStatus, isActive, isHistoricalPractice}` for only current active diagnoses authored in this Doctor's encounters. Full notes and immutable amendment snapshots are **not** in the timeline; link to existing Encounter Details and Amendment History, guarded by original module permissions. No other Doctor's diagnoses.

### MP-05 — Own Prescription History
`GET /doctors/me/patients/{patientId}/prescriptions?practiceId=&status=&fromDate=&toDate=&pageNumber=1&pageSize=20`

Response patient-scoped, Doctor-owned list, each `{prescriptionId, encounterId, practiceId, visitDateUtc, currentVersionNumber, currentStatus, medicationCount, hasVersions, canView, canExport, isHistoricalPractice}`. Include valid issued records, including ones whose current version is Voided; exclude never-issued Drafts. Use `Prescriptions.ViewOwn` and relation/Doctor ownership; `canExport` also needs `Prescriptions.ExportOwn`, valid issued version and read scope. Reuse existing `GET /doctors/me/prescriptions/{id}` and `/versions[/n]` for full data; any adaptation for closed Practices must be read-only and preserve write guards.

### MP-06 — Doctor-scoped Patient Appointments
`GET /doctors/me/patients/{patientId}/appointments?practiceId=&status=&fromDate=&toDate=&pageNumber=1&pageSize=20`

Return own Practice Reservations including live/archive with `reservationId`, `practiceId`, `businessDate`, `scheduledStart`, `visitType`, `segment`, `status`, `linkedTicketId?`, `followUpEligibilityId?`, `isArchived`, source flags. Requires current `DoctorPracticeReservations.ViewOwn` or applicable approved historical/archive read permissions in addition to My Patients scope. Must not add reservations from other Doctors. Existing Doctor Cancel/Reschedule remain unchanged, only active Practice and live valid state, as currently authorized.

### MP-07 — Patient Follow-Up History
`GET /doctors/me/patients/{patientId}/follow-ups?practiceId=&status=&pageNumber=1&pageSize=20`

Return own `FollowUpEligibility` rows: `eligibilityId`, `sourceEncounterId`, `practiceId`, `status` (effective Available / Reserved / Consumed / Expired), `validUntil`, `reservedReservationId?`, `reservedTicketId?`, `consumedEncounterId?`, `canBook`, and server-computed `canCreateFollowUpFromSource` where meaningful. Use Doctor's `FollowUpEligibility.ViewOwn` and scoped history; avoid exposing unrelated patient eligibility. `Create FollowUp` action still calls the existing `POST /doctors/me/practices/{practiceId}/encounters/{encounterId}/follow-up-eligibility` with `{validUntil}`, from an eligible Completed Encounter only. Booking still by Patient or Reception.

### MP-08 — Prescription Version PDF (view/print/download)
`GET /doctors/me/prescriptions/{prescriptionId}/versions/{versionNumber}/pdf?disposition=inline|attachment`

- Doctor-owned issued version ONLY; require `Prescriptions.ViewOwn` + `Prescriptions.ExportOwn`, Doctor account/approval, exact source Encounter/Practice ownership and appropriate active or historical read policy. Never accept PatientId/DoctorId from caller as proof. Version must be issued and renderable. Draft or non-existent versions -> safe 404 / domain validation.
- Stream generated PDF bytes with `application/pdf`, sanitized filename, `Content-Disposition` controlled by validated disposition, `no-store`, `nosniff`, no public/media URL, no persistent unprotected copy.
- Render official-looking but **not legally certified** presentation: practice/doctor identity, patient name, encounter/date, drug names/strength/form/dosage/frequency/duration/instructions, version/status, generated timestamp. Arabic/RTL Unicode must work. Do not embed patient ID numbers/secret internal fields/audit reasons.
- `Superseded`: prominent `SUPERSEDED — NOT CURRENT`. `Voided`: prominent `VOIDED — NOT VALID`. Even valid historical prescriptions must have a document date/version; do not present as a live new medication order. Print is supported via inline PDF viewer; download via attachment. No new Print POST.
- Reauthorize immediately before bytes are returned, audit export without PHI, and fail closed if audit required by policy cannot persist. Adopt project-standard PDF service and safe stream disposal. Do not expand PDF to cross-Doctor shared prescriptions in this task.

## 4. EXISTING ENDPOINTS — REUSE, NO DUPLICATE COMMANDS

- Own Clinical: `GET /api/v1/doctors/me/practices/{practiceId}/encounters`, `GET .../encounters/{encounterId}`, `GET .../encounters/{encounterId}/amendments`, `GET .../tickets/{ticketId}/encounter`.
- Own Prescriptions: `GET /api/v1/doctors/me/prescriptions/{prescriptionId}`, `GET .../versions`, `GET .../versions/{versionNumber}`; current correction/void APIs unchanged.
- Doctor Lab: `GET /api/v1/doctors/me/lab-requests?patientId=...`, `/lab-results?patientId=...`, `/lab-result-submissions?patientId=...`; existing detail, review, history, version, attachment APIs unchanged. Radiology equivalents same shape.
- Doctor Reservations: `/api/v1/doctors/me/practices/{practiceId}/reservations[/{reservationId}]`, and existing Cancel/Reschedule GET/POST endpoints; archival read endpoints as necessary.
- Existing FollowUp creation `POST /api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/follow-up-eligibility`; Patient and Reception booking endpoints with `followUpEligibilityId`/optional `followUpEligibilityRowVersion`; no new booking writer.
- Existing Phase16 Treating Medical Record: `/api/v1/doctors/me/practices/{practiceId}/tickets/{ticketId}/medical-record/{overview|timeline|...}` and Hidden owner access-request flow; preserve all sharing gates and feature flags.

If a reused read endpoint currently rejects an inactive Practice (it often does), implement the **narrow own-historical-read code path** described below, not a frontend workaround or a generic permission bypass. Only modify reused endpoints when needed for the approved rule; retain their public DTO shape unless safely additive.

## 5. AUTHORIZATION MATRIX AND POLICY DESIGN

Proposed new granular permissions, append-only to `PermissionNames.All`, seeded deterministically and idempotently into Doctor role (no reordering/removing old names):

- `DoctorPatients.ViewOwn`: Doctor's directory, basic profile/contact, relationship metadata.
- `DoctorPatients.ViewHistoricalOwn`: additional scope for OWN read-only resources from owned inactive/closed Practices.
- `Prescriptions.ExportOwn`: download/print Doctor-owned issued prescription version PDFs (also requires `Prescriptions.ViewOwn`).

**All source-specific permissions still required** for protected clinical and operational content: `MedicalEncounters.ViewOwn`, `Diagnoses.ViewOwn`, `Prescriptions.ViewOwn`, `LabRequests.ViewOwn`, `LabResults.ViewOwn`, `LabResultSubmissions.ViewOwn`, Radiology counterparts, `FollowUpEligibility.ViewOwn`, Doctor Reservation and Ticket view rights, archive rights where required. Never let `DoctorPatients.ViewOwn` substitute for a medical record permission. Do not grant SuperAdmin or Reception medical access by default. Do not create a Doctor-owned Patient copy.

Implement reusable `DoctorPatientRelationshipPolicy` and `DoctorOwnHistoricalReadPolicy` in Application with repository-driven current DB actor snapshot. Relationship proof from durable accepted Ticket provenance, subject to scope. Historical policy must be a separate *read decision* that checks actor + current effective permission + `source.DoctorId` + owned original Practice + related Patient, and explicitly returns a `readOnly` context. Any existing `OwnPracticeAsync`/`DoctorAsync(practiceId, permission)` methods used by writers MUST retain active Practice requirement. Introduce named `AuthorizeOwnReadAsync`/`AuthorizeOwnHistoricalReadAsync` semantics as appropriate; don't overload generic methods that writer handlers also use. Use constant-time opaque denial of unauthorized IDs; safe 404 for unrelated resource, 401 for anonymous, 403 for insufficient actor-level permission. Do not leak target Patient existence.

In particular review and correctly restrict `MedicationAccess.OwnPracticeAsync`, `GetDoctorPrescriptionHandler`, `ListPrescriptionVersionsHandler`, `ClinicalAccessService.DoctorAsync`, `ClinicalQueries`, `GetTicketDetailsQueryHandler`, `DiagnosticReadService`'s `LabRequests/LabResults/LabSubmissions` and Radiology equivalents, archive authorization/filters, encounter details/read audit, attachment streaming and the server-calculated `Capabilities`. A read from a closed Practice must not expose an enabled mutating capability or call a writer-only access guard. Test the *existing mutation APIs* against closed Practices after the changes.

Do not relax Phase16 `IMedicalRecordAccessPolicy` / `MedicalRecordScope` current treatment / approved hidden grant. Historical own access is NEVER a treating-other-Doctor scope. Medical data and export downloads always require fresh authorization/audit, not only list membership at page load.

## 6. DATABASE, ARCHIVES, QUERY PERFORMANCE

- Inspect actual `TicketRegistry`, `AllTickets` (live + archived), `Ticket` check-in creation, `DoctorPractices`, `MedicalEncounters` and approved archive models. Prefer immutable `TicketRegistry` identity for cheap membership qualification, but use authoritative Ticket check-in data to calculate timestamps/counts and validate legacy rows. Do not double-count a Ticket while being archived. Do not use Reservation or Patient registration provenance as membership.
- All-time membership remains valid after operational archival and Practice closure. No implicit cutoff/date window for directory membership.
- SQL group/aggregate by DoctorId/PatientId; unique stable pagination and totals. Use AsNoTracking, projections, bounded filters, cancellation tokens, no `ToList()` over unbounded Tickets, no per-patient N+1. Use DB-side bounded joins for patient/contact and optional clinical stats. Handle missing/deleted contact values without throwing.
- Inspect current indexes before proposing new ones. Add additive EF Core SQL Server migration for necessary composite indexes (candidate: `TicketRegistry(DoctorId, PatientId, BusinessDate)` with actual provider-compatible includes; useful Encounter and reservation lookup indexes). Don't add duplicate indexes or incompatible computed filters. No broad data rewrite, no invented Encounter backfill, no new business tables without documented proof of need. Index changes must be compatible with deployed archive registry and migrations.
- Maintain UTC timestamp versus Practice-local business date distinction. Use server-provided timezone info, document date filters and sort. Stable snapshots/paging under concurrent attendance/archive; no identical patient repeated on adjacent pages. Cover archive move and time-ordered data with tests.
- Optional caching only if secure, user-scoped, revocation-aware and invalidated by new Ticket, Encounter completion, Reservation/Practice changes. Safest V1 default: no cross-request PHI caching.

## 7. EXPECTED CODE CHANGE INVENTORY (VERIFY REAL PATHS)

**NEW Application feature** (names can follow current repo naming standards):
`src/Wasla/Wasla.Application/Features/DoctorPatients/DoctorPatientContracts.cs`, `DoctorPatientQueries.cs`, `DoctorPatientValidators.cs`, `DoctorPatientHandlers.cs`, `DoctorPatientRelationshipPolicy.cs`, `DoctorOwnHistoricalReadPolicy.cs`, interfaces for `IDoctorPatientReadService` and secure `IPrescriptionPdfRenderer` as appropriate.

**NEW Infrastructure**: `.../Persistence/DoctorPatientReadService.cs`, PDF implementation integrated with existing project rendering/media conventions and DI. Index migration only if evidenced.

**NEW API**: `src/Wasla/Wasla.Api/Controllers/DoctorPatientsController.cs` and a narrowly scoped own-prescription PDF read endpoint in existing medication controller or separate own-prescription export controller. Map all MP-01..MP-08 in Swagger and OpenAPI.

**MODIFY Security/Seeding**:
`src/Wasla/Wasla.Domain/Security/SystemSecurity.cs` and `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/WaslaSecuritySeeder.cs`; current doctor access snapshot/permission checks. Do not alter existing permission ID derivation/order or grant medical reads to new roles.

**MODIFY only as necessary for inactive-Practice OWN READS**:
`src/Wasla/Wasla.Application/Features/Clinical/ClinicalAccessService.cs`, `ClinicalQueries.cs`, `ClinicalMutations.cs` **only to preserve blocked writes**, `src/Wasla/Wasla.Application/Features/Medications/MedicationAccess.cs`, `PrescriptionHandlers.cs`, `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Persistence/ClinicalReadService.cs`, `DiagnosticReadService.cs`, `src/Wasla/Wasla.Application/Features/Tickets/GetTicketDetails/GetTicketDetails.cs`, relevant reservation/archive guards/read services and download handlers. Inspect the full call graph: a change to a shared authorizer must not open write handlers.

**REUSE unchanged whenever possible**:
`ReservationsController.cs`, `ClinicalControllers.cs` FollowUp command, `MedicationControllers.cs` correction/void routes, `LabControllers.cs`, `RadiologyControllers.cs`, `HiddenAccessControllers.cs`, Phase 16 `MedicalRecordAccessPolicy` sharing gates, existing Queue/Check-In/Walk-In/Payment/Archiving writes.

**TESTS**: new unit/integration/architecture/SQL Server cases under current test projects, contract/schema checks, PDF byte tests; no requirement for a full web UI in this backend task. Provide frontend handoff with ready route/sample DTO shapes.

## 8. DETAILED TEST / EDGE-CASE REQUIREMENTS

### Membership, identity and integrity
- Reception creates Basic Patient; no Ticket => not listed. Reservation without check-in => not listed. Successful Reservation Check-In or Walk-In => listed exactly once.
- Tickets in Waiting, Called, InProgress, Completed, Cancelled/NoShow after actual check-in still count as *attendance*, not completed examination.
- Same Patient across 1/2/3 owned Practices => one Doctor-level row, accurate count and last Practice. Same Patient treated by a different Doctor => only that Doctor's independent relationship and records; no cross-leak.
- Patient account unlinked/closed/updated name/contact remains the same global Patient; reflect latest centralized basic identity, no fuzzy merge; phone/email nullable for minors; no guardian details by default.
- Archived Ticket and mixed live/archive pages => same membership/count and correct Visit History; no double count during archive race; old operational Ticket with no Encounter does not acquire synthetic clinical data.
- Search by Arabic/English/normalized phone/email is restricted to relationship scope and paginated. Unknown or random PatientId => 404; filters from unrelated Practice must not enumerate another Doctor's patients. Invalid date range/oversize search/pagination => localized validation response.

### Privacy and permissions
- Anonymous, Reception, Patient and SuperAdmin cannot invoke Doctor-only My Patients routes merely through role or arbitrary GUID.
- Inactive user, first-login user, Pending/Rejected/Suspended Doctor, revoked DoctorPatients permission fail closed; current DB grants checked even with stale JWT.
- Directory contact permitted under `DoctorPatients.ViewOwn`; other Doctors' contacts cannot be searched. Missing clinical or reservation permission => no sensitive data or sensitive counts in overview; module-specific detail access still independently denies.
- `DoctorPatients.ViewHistoricalOwn` absent => no reading own clinical data from closed Practice. With it, Approved+active Doctor can read ONLY own historical content and export own issued Rx if source permissions permit. All existing writes to closed Practice still fail, including amendments, Rx corrections/voids, Lab/Radiology reviews/uploads, FollowUp create, Ticket/Reservation writes, even if stale capabilities or manually crafted payloads.
- Cross-Doctor `Visible` outputs only during matched active InProgress treating Encounter + Phase16 gate; end treatment immediately revokes. `Hidden` date-only placeholders and explicit owner approval unchanged; wrong ticket/doctor/visibility revision revokes. No drafts, other-Doctor private notes, pending submissions, unauthorized attachment downloads, or other-Doctor PDFs.
- Audit/PHI logs: own Encounter/Prescription/diagnostic sensitive reads + PDF and attachment downloads audited with metadata only, no patient names, diagnoses, medicine text or raw file keys. Revoked access or failed audit must not leak bytes. Cache headers disable storage.

### Lifecycle and outputs
- Completed clinical encounter counting differs from attendance; InProgress diagnosis vs completed values follow existing rules; changes to Completed Encounter only via current amendments.
- Own PDF for Finalized, Superseded, Voided status: starts with `%PDF`, correctly encodes Arabic/RTL, contains correct status/version/watermark, cannot be generated for drafts, is authorized again on download; inline/attachment content dispositions safe; no persistent public URL. No legally certified signature claim.
- Patient PendingReview result submission appears only in own Doctor review history, not official results or cross-doctor sharing; accepted result becomes official; rejected/voided/corrected status reflected; download authorization checked.
- Doctor sees patient past/upcoming reservations only at owned Practices and accurate follow-up states Available/Reserved/Consumed/Expired. Create eligibility only from eligible Completed Encounter; Patient/Reception book, not Doctor from My Patients. Booking, quota, pricing, Idempotency, optimistic concurrency and Existing FollowUp gates unchanged.
- Performance testing on representative synthetic data: thousands of patients, tens/hundreds of thousands of tickets, multi-practice, archive mix, 100-size pages, heavy search. Assert bounded SQL query count/no client-side aggregation and stable ordering; publish results, not fabricated timings.

### Release/security regression
- Run existing relevant Phase 5/10–18 clinical/identity/archiving/security tests, unit/integration/architecture plus SQL Server-backed scenarios where available. Verify no permissions/seed IDs shifted, migrations reversible or controlled under existing policy, Swagger route/DTO health. Production and Staging cross-doctor flags remain disabled. No DB migration applied to production by this task.

## 9. IMPLEMENTATION ORDER / DELIVERABLES

**Step 1 — analysis and documented traceability:** current file/endpoint inventory; record approved rules; additive Source of Truth addendum; list existing vs new contracts and permission matrix. Confirm exact inactive Practice call paths and archive read behavior before code.

**Step 2 — security foundations:** append-only PermissionNames + idempotent Doctor seeding, DoctorPatient relationship policy, explicit own-historical-read policy; unit tests of denied/allowed states BEFORE exposing new routes.

**Step 3 — MP-01/02:** read-model SQL, directory stats/filters/search, overview and authorizations; tests.

**Step 4 — MP-03/04:** unified Ticket/Archive attendance history, diagnosis timeline, read-only links to existing details and amendment workflows; tests.

**Step 5 — MP-05/08:** prescription patient-scoped list, read-only closed Practice access to existing Doctor prescription/versions, secure PDF renderer/endpoint and file/authorization tests.

**Step 6 — MP-06/07:** appointments and FollowUp read model; reuse existing FollowUp create and Reception/Patient booking. No new writer.

**Step 7 — own historical clinical Lab/Radiology read support:** guarded minimal adaptations of existing Doctor get/list/version/submission/download readers; all writers remain locked for inactive Practice; regression.

**Step 8 — regression and release artifacts:** OpenAPI/Swagger, route-response examples, frontend handoff map for 10 screens, archive/PDF/clinical performance and security test evidence, controlled migration scripts if used, before/after file summary and implementation report.

## 10. NON-NEGOTIABLE EXIT CHECKLIST

- [ ] MP-01..MP-08 implemented and documented; all new IDs and patient scope validated server-side.
- [ ] Owner-approved rules in this task adopted in a tracked addendum; no fabricated Master update/signoff.
- [ ] Patient remains global; no `DoctorPatient` ownership table or copied medical records.
- [ ] Every qualifying historical attendance appears once per Doctor; archived and closed Practice records preserved.
- [ ] All source-specific permissions + current DB account status + Doctor approval enforced; no hidden privacy bypass.
- [ ] Closed Practice own historical reads work with explicit historical permission; **every existing closed Practice writer remains denied**.
- [ ] Existing shared-medical-record active-treatment-only gates unchanged, Prod/Staging sharing disabled.
- [ ] Own issued prescription version PDF works for Arabic/RTL, status marks, no drafts, private streaming and audit.
- [ ] No duplicate FollowUp/Booking/Review/Correction commands; use existing workflow.
- [ ] Unit + integration + architecture + SQL Server-backed security/archive cases pass; report exact counts and blockers, never claim tests passed without execution.
- [ ] Build/Swagger/migration docs and frontend API handoff updated, with all deviations called out.

**Codex reporting requirements:** Begin with a concise inspection/impact plan; then implement in the order above. At finish, provide actual changed file paths, exact new routes/response DTOs, existing route behavior affected, migrations/indexes, permission/seed deltas, all test commands and outputs, outstanding risks/release gates, and a specific frontend handoff mapping `Screen -> API -> required permission -> states/actions`. Do not ask for previously approved design decisions. If real code or the authoritative Master contradicts the task, stop only that conflicting modification, describe old rule/new rule/impact, and keep it pending under change control; continue safe nonconflicting work.

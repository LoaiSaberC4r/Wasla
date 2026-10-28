# Doctor My Patients V1 implementation report

Date: 2026-10-28. Repository: `LoaiSaberC4r/Wasla`. Branch: `codex/doctor-my-patients-v1`, created from clean `main` at `a019a0cf9be548cb8918e5513b5b67b062e23826`.

The backend implements the eight approved GET endpoints, a Doctor-wide actual-attendance relationship directory, protected patient history, explicit own historical reads at closed Practices, and private issued-prescription version PDFs. Existing Clinical, Prescription, Diagnostic, Reservation, Follow-Up and Archive commands remain the mutation workflows. No DoctorPatient aggregate, copied Patient profile, Doctor booking command or new cross-Doctor sharing path was introduced.

## Source review and rule traceability

The complete supplied task is retained as [WASLA-CODEX-DOCTOR-MY-PATIENTS-V1.md](WASLA-CODEX-DOCTOR-MY-PATIENTS-V1.md). Repository Phase 5 and 10–18 addenda, source implementations, BuildingBlock authorization/CQRS/persistence/error conventions and relevant tests were inspected. The Master `Healthcare-Platform-Master-Source-of-Truth-v1.2-Updated.txt` was absent, including a hidden/ignored-file search excluding generated build/Git folders. It was not read or edited; no formal Master adoption or legal sign-off is claimed.

The [additive Source of Truth record](SOURCE-OF-TRUTH-DOCTOR-MY-PATIENTS-V1-ADDENDUM.md) maps MP-BR-01..12 to the implementation, distinguishing CONFIRMED, APPROVED IN THIS TASK, PROPOSED TECHNICAL DESIGN and DEFERRED work. The frontend/backend contracts document the following compatibility decisions explicitly:

- The five history pages reuse `ClinicalPage<T>` rather than introducing duplicate page envelopes. Their concrete schemas are declared in OpenAPI.
- Directory responses add `asOfUtc`. Reuse it for adjacent pages to anchor admission ordering under new check-ins. This is a timestamp cutoff, not a cached identity/authorization snapshot; refresh after Practice governance changes.
- A prescription history row represents the current issued version (Finalized/Voided). Superseded issued versions remain available through the original version readers and the new PDF route. Drafts are absent from issued history/export.
- Phase 18's original own archived Reservation detail supports standalone bookings without prior admission. That existing endpoint retains exact booking ownership authorization. The seven new patient routes always require actual Ticket admission, including the new appointments tab. This preserves the approved old booking-history behavior without creating directory membership.
- Existing practice-scoped Clinical endpoints retain their established opaque foreign-Practice 403 response. New unrelated patient/resource IDs receive safe 404 responses.

No unresolved conflict with an available approved rule was identified. Master reconciliation remains external because that artifact is absent.

## Implementation and security boundaries

| Area | Change and reason |
| --- | --- |
| Membership | Existing live/archive Ticket union plus permanent TicketRegistry identity, matching owned Practice and valid Walk-In/Reservation check-in provenance. Later Ticket status never erases admission. Reservation/registration alone never qualifies. |
| Directory | SQL grouping by PatientId, scoped search/filter/order/counts, 1..100 page size, all-time summary, current global identity/contact, nullable protected counts, no guardian/evidence/key exposure. No in-memory all-ticket aggregation or per-patient query loop. |
| Actor | Current authenticated user must match an active, first-login-complete, Approved Doctor in the DB. Effective grants intersect current DB grants with token grants. DoctorId is derived, never accepted as input. |
| Patient tabs | Actual attendance proof before reading the source, exact Doctor ownership, original source grants and archive grants where applicable; fresh actor/grant checks and required metadata-only audits before response. |
| Historical reads | A named read-only policy adds closed-Practice own access with `DoctorPatients.ViewOwn`, `DoctorPatients.ViewHistoricalOwn` and the original source grant. Practice state is queried without tracking. No generic writer authorizer was relaxed. |
| Existing source readers | Own Encounter details/amendments, prescriptions/versions, diagnostics/results/submissions/media, Ticket and Reservation details support authorized closed sources. Drafts and every inactive-Practice write/visibility capability are suppressed. Idempotency replays retain active-Practice writer guards. |
| Source separation | Ticket detail cannot imply clinical or financial access. Encounter references require original Encounter and Diagnosis read rights; prescription references require their read right; financial identifiers/refund capabilities retain their separate rights. |
| PDF | Exact own issued version and completed source Encounter, fresh permission/Practice/status checks after rendering, mandatory audit before bytes. Encoded HTML, JavaScript disabled, offline rendering and aborted requests prevent external asset fetching. No public export URL or persisted export file. |
| Privacy | No-store/nosniff responses, bounded existing rate policies, metadata-only immutable audit category restricted by the existing sensitive-audit path. No added PHI cache, payload logging, contact fallback or medical-sharing grant. |
| Sharing | Existing Phase 16 treating Ticket/InProgress Encounter/identical Patient/current permission/configuration/revision-bound hidden-access rules remain in place. Staging and Production `CrossDoctorSharingEnabled` remain false; those files were not modified. |

The PDF includes bilingual Arabic/English headings, isolated mixed-direction clinical values, medication details, Doctor/Practice/Patient names, visit/issuance dates and version. Superseded/Voided documents have prominent invalid-current-treatment warnings, a fixed watermark and status/version/page footers. Draft/unknown states fail closed. Output is limited to the existing 15 MiB PDF limit. Actual Chromium PDFs for all three issued statuses were rendered and visually checked with Poppler; Arabic was readable without clipping or reversed value/label order. These synthetic QA documents are local test artifacts, not patient exports or legal certification.

## API contracts

All routes start `/api/v1`, require authentication, current Approved/active Doctor state and their source scope. The seven patient routes also require `DoctorPatients.ViewOwn` and actual attendance relationship. Full fields, nullability, filters, error semantics and affected old reads are in [API changes](DOCTOR-MY-PATIENTS-V1-API-CHANGES.md), the [generated OpenAPI subset](docs/doctor-patients/openapi.json) and [synthetic examples](docs/doctor-patients/response-examples.json).

| ID | GET route | Response | Additional read/export grant |
| --- | --- | --- | --- |
| MP-01 | `/doctors/me/patients` | `DoctorPatientsPageResponse` | Source grants for optional protected fields/filters |
| MP-02 | `/doctors/me/patients/{patientId}` | `DoctorMyPatientOverviewResponse` | Each included source section's grant |
| MP-03 | `/doctors/me/patients/{patientId}/attendances` | `ClinicalPage<DoctorPatientAttendance>` | `DoctorPracticeTickets.ViewOwn`, archive right for archived rows |
| MP-04 | `/doctors/me/patients/{patientId}/diagnoses` | `ClinicalPage<DoctorPatientDiagnosis>` | `Diagnoses.ViewOwn` |
| MP-05 | `/doctors/me/patients/{patientId}/prescriptions` | `ClinicalPage<DoctorPatientPrescription>` | `Prescriptions.ViewOwn` |
| MP-06 | `/doctors/me/patients/{patientId}/appointments` | `ClinicalPage<DoctorPatientAppointment>` | `DoctorPracticeReservations.ViewOwn`, archive right for archived rows |
| MP-07 | `/doctors/me/patients/{patientId}/follow-ups` | `ClinicalPage<DoctorPatientFollowUp>` | `FollowUpEligibility.ViewOwn` |
| MP-08 | `/doctors/me/prescriptions/{prescriptionId}/versions/{versionNumber}/pdf` | Private `application/pdf` bytes | `Prescriptions.ViewOwn` AND `Prescriptions.ExportOwn`, exact own source/attendance proof |

Closed source reads additionally require the explicit historical grant. New patient endpoints cannot authorize other Doctors' notes, drafts, pending results or medical records. PDF `disposition=inline|attachment` uses a server-generated ASCII GUID/version filename. Errors use existing localized ProblemDetails: 401/403/404/409/422/429, and controlled 503 for unavailable PDF rendering. Directory/tab reads reuse `medical-record` rate limiting; PDF reuses `reports-export` and bounded renderer concurrency/timeouts.

## Permissions and seeding

| Permission | Deterministic PermissionId | Default role |
| --- | --- | --- |
| `DoctorPatients.ViewOwn` | `20000000-0000-0000-0000-000000000322` | Doctor |
| `DoctorPatients.ViewHistoricalOwn` | `20000000-0000-0000-0000-000000000323` | Doctor |
| `Prescriptions.ExportOwn` | `20000000-0000-0000-0000-000000000324` | Doctor |

The three names append to the existing 321-permission sequence. Existing IDs/order remain intact. The existing idempotent security seeder adds missing permission/grant rows only; the contract test invokes it twice and verifies no duplicate grants. No Reception/Patient/SuperAdmin default medical grant or Reception assignment capability is added. Refresh/reissue Doctor tokens after seeding; stale tokens cannot acquire a new grant from the DB alone.

## Schema and controlled deployment

Migration: `20261028111250_DoctorMyPatientsReadIndexes` with matching Designer/snapshot. It adds six lookup indexes:

- `TicketRegistry(DoctorId, PatientId, BusinessDate)`.
- Live and archived `Tickets(DoctorId, PatientId, CheckInTimeUtc, Id)`.
- Live and archived `Reservations(DoctorId, PatientId, ScheduledStartUtc)`.
- `MedicalEncounters(DoctorId, PatientId, StartedAtUtc)`.

The three old single-column DoctorId indexes on TicketRegistry/live Tickets/archived Tickets are replaced by the corresponding DoctorId-prefix composites. No business table, ownership row, data rewrite or clinical-record copy is added. `Down` removes the six composites and restores those three indexes.

Reviewed artifacts: [idempotent forward SQL](docs/doctor-patients/migration.sql) from `20261025120012_Phase17VerifiedNotificationContacts`; [rollback SQL](docs/doctor-patients/rollback.sql). EF reports no pending model changes. The complete integration run passed the existing nonempty-baseline SQL migration rehearsal: it migrated through the latest migration, repeated migration without pending work, exercised archive backfill/restarts and inspected the archive index plan. This exercised the new forward migration on an isolated SQL database. The new rollback SQL was generated and inspected, but was not executed. No production/staging migration was performed.

Release sequence: review backup/index-build locking and IO for the target database; apply the controlled migration through the existing deployment process; run the existing security initialization/seeder; refresh Doctor tokens; configure an installed Chromium absolute executable path through `Phase18:ChromiumExecutablePath` / `Phase18__ChromiumExecutablePath`; verify Arabic fonts and private PDF delivery in the deployment environment. Existing `Phase18` timeout/concurrency settings are reused (45 seconds/2 renders by default). No browser is downloaded at request time. Keep Staging/Production cross-Doctor sharing flags disabled.

## Verification

Environment: Windows, .NET SDK 10.0.401/runtime 10.0.12, EF Core 10.0.10, local SQL Server Express with Windows authentication, and installed Chrome. Tests used disposable fixture databases. The existing test file-log sink used a process-local shared-file setting during final runs; no application configuration change was made. Secrets and patient payloads are excluded from published evidence.

| Check | Verified result |
| --- | --- |
| Final solution build | 0 warnings, 0 errors |
| Final Wasla unit suite | 336 passed, 0 failed/skipped, including the paging-anchor underflow regression |
| Final architecture suite | 26 passed, 0 failed/skipped |
| BuildingBlock complete rerun, including SQL cases | 323 passed, 0 failed/skipped |
| Initial complete integration run | 383 executed: 379 passed, 4 failed, 0 skipped; failures and fixes are recorded below |
| Focused rerun of all four failing integration cases plus related cases | 6 passed, 0 failed/skipped |
| Feature integration filter after reader hardening | 26 passed, 0 failed/skipped: 25 feature cases and 1 existing governance case whose name matches the filter |
| Final affected-source/feature rerun | 30 passed, 0 failed/skipped; includes both providers and the last read-authorization changes |
| Fresh complete integration run against the last read-authorization build | 385 passed, 0 failed/skipped, 33.75 minutes; the final validation-only change below was checked separately |
| Final directory API rerun after the anchor bound | 11 passed, 0 failed/skipped, including the additional early-timestamp 422 case |
| EF model consistency | No changes since last migration |
| OpenAPI | Eight GET contracts, concrete response schemas, private headers, binary PDF and anonymous denial checked |
| Git whitespace validation | `git diff --check` clean |

The initial integration failures were: an overly strict admission check on the old standalone archived Reservation detail (corrected to preserve Phase 18 ownership semantics); the minimal Phase 12 component fixture missing the new read-service registration/current DB grants (fixture completed without weakening assertions); an existing Phase 16 1,000-visit query-plan test timing out under the broad run; and one SQL NoShow restore case timing out. Every failing case passed on focused rerun, including the real Phase 16 query-plan check. The initial architecture failure exposed a frozen constructor dependency rule and was fixed by using the existing read repository. An initial BuildingBlock 50 ms cache-expiry assertion failed under concurrent load; the unchanged complete BuildingBlock suite passed when rerun separately. These failed attempts are retained in the evidence rather than described as successful runs.

Final review added one validation-only bound: an extremely early UTC paging anchor cannot leave the representable date range when the summary subtracts 30 days. It returns validation failure instead of a date-overflow error. A registered-validator unit regression checks this boundary and rejects unspecified timestamps; the final solution build and 11-case directory API rerun passed after that change. No source reader, writer or SQL projection changed for this bound. The complete 385-case integration run started before this final bound and its additional HTTP test. Across that complete run and the final API rerun, 386 unique integration cases have passing latest outcomes; a single post-edit 386-case full run was not repeated.

New acceptance coverage includes global/Basic/minor/null-contact identity; reservation-only exclusion; real admission; all terminal Ticket statuses; multi-Practice deduplication; scoped Arabic/English/phone/email search; safe unrelated IDs and actor denials; current-grant revocation/null protected fields; archive moves; own closed Encounter/diagnosis/prescription/diagnostic/reservation reads; false closed-Practice capabilities and denied existing writes/idempotency replays; issued-version correction/void/history; actual PDF bytes and disposition; failed-audit/revocation/Practice closure during rendering; attachment authorization; idempotent Doctor-only seeding; OpenAPI; and SQL-side bounded directory pagination. Existing Phase 5/10–18 regression suites cover identity/claim/family, concurrency, booking/pricing, Follow-Up and Phase 16 sharing/hidden-access behavior.

SQL scale test: 2,001 unique patients and 20,002 tickets after a concurrent new admission, two owned Practices, live/archive mix, page size 100, 21 distinct pages. Latest measured first-page reader time was 3,232.1 ms, with exactly five reader SQL queries (three summary counts, filtered count and page), and 313.2 seconds for synthetic seed setup. Auth/audit queries are outside that reader count. Heavy scoped search returned one expected patient; pagination with `asOfUtc` returned every expected patient once despite the new check-in. The archive test covers an actual move and mixed pages; this is not a production load/concurrency SLA or an index-before/after benchmark. [Captured parameterized SQL](docs/doctor-patients/directory-query.sql) demonstrates GROUP BY/OFFSET/FETCH; it is query text, not a new directory execution-plan artifact. The separate existing Phase 16 plan-inspection regression passed.

Commands, exact run counters, hashes and case outcomes are published in [verification.json](docs/doctor-patients/verification.json). [verify.ps1](docs/doctor-patients/verify.ps1) reproduces the sequential build/full-suite/model checks against a supplied test SQL Server and installed Chromium; this convenience script was syntax-checked, while the equivalent individual commands produced the recorded test results. Raw console logs/TRX and synthetic PDF/PNG QA remain locally under ignored `_local-data/doctor-patients-validation`; the machine-readable evidence records their hashes and relative paths without committing large logs or credentials.

## Changed files and frontend handoff

The [complete changed-file manifest](docs/doctor-patients/changed-files.md) lists every new/modified path. Main additions are the Application `Features/DoctorPatients` contracts/validators/policies/handlers, infrastructure SQL reader and PDF renderer, API controllers/OpenAPI filter, lookup migration, 26 feature integration cases (including the final anchor-boundary HTTP case), 16 unit cases and two architecture cases. Existing source adapters and fixture corrections are listed separately from new feature files.

The [frontend handoff](DOCTOR-MY-PATIENTS-V1-FRONTEND-HANDOFF.md) maps all ten requested screens to exact API families, permissions, states and existing actions. It covers null/unavailable counters, closed-Practice banners, Ticket-versus-Encounter distinction, version/status PDF handling, diagnostic PendingReview versus official results, Follow-Up creation versus Patient/Reception booking, current-treatment-only sharing and immediate client state removal after revocation. No frontend implementation was requested or added.

## Remaining deployment and external gates

No unresolved backend implementation blocker is known. The missing Master cannot be reconciled or formally updated here. Production rollout, target-volume SQL performance/locking acceptance, deployed Chromium/fonts and the inherited credential hygiene gate documented in earlier repository security records remain deployment responsibilities. No credential rotation, production migration, deployment, push or pull request was performed. Legally certified e-prescribing/signatures, new booking/writer/notification policies and retention-policy changes remain deferred as the approved task specifies.

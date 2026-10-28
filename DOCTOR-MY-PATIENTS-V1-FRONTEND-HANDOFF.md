# Doctor My Patients V1 frontend handoff

Use the current authenticated Doctor session. Never send a DoctorId, copy a Patient profile, or infer a completed examination from Ticket status. Fetch only the visible authorized section; do not prefetch shared records. All paths below start `/api/v1`.

After the idempotent permission seeder runs, refresh/reissue the Doctor's token to carry the three new grants. Both token and current DB permissions must authorize the request.

| Screen | Read API | Required permission/scope | States and existing actions |
| --- | --- | --- | --- |
| 1. Patient directory | `GET /doctors/me/patients` | `DoctorPatients.ViewOwn`; approved/active DB actor | All-time initial list, scoped name/phone/email search, owned Practice/date/minimum/upcoming/scope filters, page size <=100. Reuse returned asOfUtc on subsequent pages. Display Returning from attendanceCount>=2, historical badge, null protected counts as unavailable. |
| 2. Patient overview | `GET /doctors/me/patients/{patientId}` | Directory relationship + each source section's original read right | Current read-only identity/contact, no guardian fallback; cards from nullable authorized fields and capabilities. No profile editor. Historical-only banner when marked; source reads may need historical permission even when another attended Practice is active. |
| 3. Attendances and Ticket history | `GET .../patients/{patientId}/attendances`; existing `GET /practices/{practiceId}/tickets/{ticketId}` | `DoctorPracticeTickets.ViewOwn`; archive right for archived Ticket; historical right for closed Practice | Waiting/Called/InProgress/Completed/Cancelled/NoShow all represent an admitted Ticket. Separate encounter link, shown only with canViewEncounter. Legacy no-Encounter row is valid. Existing audited operational detail supplies timeline; inactive/archived operations disabled. |
| 4. Encounter details and amendments | Existing `GET /doctors/me/practices/{practiceId}/encounters/{encounterId}`, `/amendments`, or `/tickets/{ticketId}/encounter` | `MedicalEncounters.ViewOwn` + `Diagnoses.ViewOwn`; own Practice/source; historical right when inactive | Full own notes only here. InProgress commands and Completed amendment workflow remain the existing ones. Follow server capabilities; inactive Practice reports false for every write/visibility/create action. |
| 5. Diagnosis timeline | `GET .../patients/{patientId}/diagnoses` | `Diagnoses.ViewOwn`; own relation/source; historical right when needed | Current active diagnoses only, newest Encounter first. No private notes or amendment snapshots in timeline. Guard Encounter/amendment navigation with both original read permissions; removed diagnoses remain in immutable amendment history. |
| 6. Prescriptions, versions and PDF | `GET .../patients/{patientId}/prescriptions`; existing `GET /doctors/me/prescriptions/{id}` and `/versions[/n]`; new `/versions/{n}/pdf?disposition=inline|attachment` | `Prescriptions.ViewOwn`; PDF also `Prescriptions.ExportOwn`; own issued version and active/historical read scope | Show current Finalized/Voided records, drill into immutable Superseded versions. View/Print uses inline PDF, Download uses attachment. Do not export a Draft. Mark superseded/voided as not current/not valid. Reuse original correction-draft/finalize/discard/void endpoints only when their capabilities permit; closed Practice disables them. |
| 7. Lab/Radiology and submission review | Existing `GET /doctors/me/{lab|radiology}-requests?patientId=...`, `-results?patientId=...`, `-result-submissions?patientId=...`; existing detail/history/version/attachment APIs | Independent `LabRequests.ViewOwn`, `LabResults.ViewOwn`, `LabResultSubmissions.ViewOwn` and Radiology counterparts; exact requesting Doctor; historical right when needed | PendingReview belongs to the original request and is not official output. Existing `POST ...-result-submissions/{id}/accept` selects covered own request items; `/reject` requires patient-visible reason/current token. Use original multipart upload/correction APIs. All reviews/uploads/corrections disabled for closed Practice, regardless of old client state. Media is freshly authorized and privately streamed. |
| 8. Appointments | `GET .../patients/{patientId}/appointments`; existing own Practice reservation detail | `DoctorPracticeReservations.ViewOwn`; archive and historical rights where applicable | Past/upcoming and standalone bookings are included only after My Patients membership exists. Separate BusinessDate from scheduledStart UTC; visit type/segment are snapshots. Read closed detail with disabled capabilities. Existing Doctor Cancel/Reschedule is available only for active Practice/live valid state and original permission; no Doctor booking action. |
| 9. Follow-Up history | `GET .../patients/{patientId}/follow-ups`; existing source Encounter details | `FollowUpEligibility.ViewOwn`; historical right for closed scope | Display effective Available/Reserved/Consumed/Expired and claim references. Doctor never books here: canBook=false. Existing eligibility rows cannot issue another entitlement from the same source. If an active own Completed Encounter has canCreateFollowUpEligibility, call existing `POST /doctors/me/practices/{practiceId}/encounters/{encounterId}/follow-up-eligibility` with `{validUntil}`. Patient/assigned Reception then use original availability/booking commands and eligibility token. |
| 10. Shared medical record during current treatment | Existing `/doctors/me/practices/{practiceId}/tickets/{ticketId}/medical-record/{overview|timeline|...}` and owner hidden-access workflow | Current active matching Ticket + InProgress Encounter + identical patient + valid Practice scope + `MedicalRecords.ViewTreatingPatient` + enabled Phase 16 sharing + rechecks | My Patients membership grants no shared entitlement. Keep this action unavailable until the real treating context and existing sharing gate authorize it. No directory-level prefetch, export, unscoped patient route or persistent PHI cache. Completion immediately ends entitlement. Hidden stays a date-only placeholder until exact revision-bound owner approval; never reveal private notes/drafts/pending submissions. Staging/Production flags remain disabled. |

## Request examples

```http
GET /api/v1/doctors/me/patients?practiceScope=all&pageNumber=1&pageSize=20
GET /api/v1/doctors/me/patients?search=%D9%85%D8%B1%D9%8A%D8%B6&minAttendanceCount=2
GET /api/v1/doctors/me/patients/{patientId}/attendances?fromDate=2026-01-01&toDate=2026-10-28&pageSize=20
GET /api/v1/doctors/me/patients/{patientId}/appointments?status=Active
GET /api/v1/doctors/me/patients/{patientId}/follow-ups?status=Available
GET /api/v1/doctors/me/prescriptions/{prescriptionId}/versions/1/pdf?disposition=attachment
```

Directory response envelope:

```json
{
  "items": [],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "summary": {"allTimeUniquePatients": 0, "attendedLast30Days": 0, "returningPatients": 0},
  "asOfUtc": "2026-10-28T09:00:00Z"
}
```

The zero values above illustrate an empty relationship view; they are not production data. Non-empty synthetic examples for all eight routes are in [response-examples.json](docs/doctor-patients/response-examples.json). Exact item fields and nullability are in the [API changes](DOCTOR-MY-PATIENTS-V1-API-CHANGES.md) and the [generated OpenAPI subset](docs/doctor-patients/openapi.json).

## Client state and navigation

1. Keep source IDs and practiceId together; every endpoint reauthorizes them. There is no Doctor-owned Patient copy and no patient-level mutation token.
2. Use server capabilities for actions, then accept 403/404 as immediate revocation. Clear inaccessible patient/source data and any open private PDF/object URL. Revoke temporary object URLs on close/logout; do not place PDFs in public storage or browser persistence.
3. A null protected counter is unavailable, never zero. If the historical permission is absent, membership still appears but closed source details are unavailable. `historicalOnly=false` does not grant writes on a specific closed Practice.
4. A Ticket can have no Encounter. Do not create a synthetic Encounter or show a clinical-completed badge from attendanceCount/Ticket status. Diagnosis history is current active content; immutable removals are shown only through the existing amendment reader.
5. Use server-returned Practice-local BusinessDate for date filters and timezone-aware display for UTC instants. A response's paging anchor stabilizes admission ordering; refresh after Practice scope/governance changes. Current permissions are never frozen by the anchor.
6. Existing command idempotency keys and root RowVersion contracts are unchanged. For 409, refetch the exact source before offering a retry; do not overwrite or reuse stale capability state. For PDF 409, fetch the current version status before requesting fresh bytes.
7. Handle 401/403/404/409/422/429/503 distinctly. PDF service unavailable is a recoverable operational state. A PDF is a dated issued version, not legal certification or a new prescription order.

No frontend implementation is included in this backend task.

## Existing source calls used by this workspace

These are existing APIs, checked against the generated application Swagger. All paths below start `/api/v1/doctors/me`. Source read grants remain separate; closed-Practice source reads additionally require the historical grants. Commands still require their original mutation permissions, valid state, concurrency tokens and active Practice.

| Purpose | Existing call |
| --- | --- |
| Own Encounter and immutable amendments | `GET /practices/{practiceId}/encounters/{encounterId}` and `GET /practices/{practiceId}/encounters/{encounterId}/amendments` |
| Resolve an Encounter from an attendance | `GET /practices/{practiceId}/tickets/{ticketId}/encounter` |
| Prescription detail and exact issued version | `GET /prescriptions/{prescriptionId}`, `GET /prescriptions/{prescriptionId}/versions`, `GET /prescriptions/{prescriptionId}/versions/{versionNumber}` |
| Active-Practice Rx correction/void | `POST /prescriptions/{prescriptionId}/correction-draft`, `/correction-draft/finalize`, `/correction-draft/discard`, `/void`; edit draft items through the original item commands |
| Diagnostic lists for selected Patient | `GET /{kind}-requests?patientId={patientId}`, `GET /{kind}-results?patientId={patientId}`, `GET /{kind}-result-submissions?patientId={patientId}`; kind is `lab` or `radiology` |
| Diagnostic request and history | `GET /{kind}-requests/{requestId}` and `GET /{kind}-requests/{requestId}/history` |
| Official diagnostic result versions | `GET /{kind}-results/{resultId}`, `GET /{kind}-results/{resultId}/versions`, `GET /{kind}-results/{resultId}/versions/{versionNumber}` |
| Private official attachment | `GET /{kind}-results/{resultId}/versions/{versionNumber}/attachments/{attachmentId}/content` |
| Pending submission and private attachment | `GET /{kind}-result-submissions/{submissionId}` and `GET /{kind}-result-submissions/{submissionId}/attachments/{attachmentId}/content` |
| Active-Practice submission review | `POST /{kind}-result-submissions/{submissionId}/accept` or `/reject`; retain the Phase 15 body/RowVersion contract |
| Exact own booking detail | `GET /practices/{practiceId}/reservations/{reservationId}`; original `/cancel` and `/reschedule` commands only when permitted |
| Create an entitlement from an eligible Completed source | `POST /practices/{practiceId}/encounters/{encounterId}/follow-up-eligibility` with `{validUntil}` |
| Current-treatment sharing entry | `GET /practices/{practiceId}/tickets/{ticketId}/medical-record/overview` or `/timeline`; use only while the original gate authorizes the matched treatment |

The operational Ticket detail sits outside the `/doctors/me` prefix: `GET /api/v1/practices/{practiceId}/tickets/{ticketId}`. Use its independent source/archive rights. Full existing request bodies remain documented in the Phase 13–16 API documents; this task does not create alternate versions of those commands.

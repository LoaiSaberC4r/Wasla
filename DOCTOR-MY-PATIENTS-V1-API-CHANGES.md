# Doctor My Patients V1 API contracts

Base URL: `/api/v1`. All eight additions are authenticated GETs. Doctor identity comes from the current DB-validated account, never an input DoctorId. Directory and patient tabs require `DoctorPatients.ViewOwn`, active account, completed first login and Approved professional status. A patient ID is usable only after proving actual admission to this Doctor from trusted Ticket snapshots plus permanent registry identity.

Machine-readable contracts: [generated OpenAPI subset](docs/doctor-patients/openapi.json); [illustrative response examples](docs/doctor-patients/response-examples.json). The OpenAPI subset is extracted from the running test application's Swagger response. Examples contain synthetic data and are not clinical guidance.

| ID | Route after `/api/v1` | Response | Additional permission |
| --- | --- | --- | --- |
| MP-01 | `/doctors/me/patients` | `DoctorPatientsPageResponse` | Source permissions for optional fields/filters |
| MP-02 | `/doctors/me/patients/{patientId}` | `DoctorMyPatientOverviewResponse` | Source permissions for each protected section |
| MP-03 | `/doctors/me/patients/{patientId}/attendances` | `ClinicalPage<DoctorPatientAttendance>` | `DoctorPracticeTickets.ViewOwn`; archived rows also `Archives.Tickets.ViewPractice` |
| MP-04 | `/doctors/me/patients/{patientId}/diagnoses` | `ClinicalPage<DoctorPatientDiagnosis>` | `Diagnoses.ViewOwn` |
| MP-05 | `/doctors/me/patients/{patientId}/prescriptions` | `ClinicalPage<DoctorPatientPrescription>` | `Prescriptions.ViewOwn` |
| MP-06 | `/doctors/me/patients/{patientId}/appointments` | `ClinicalPage<DoctorPatientAppointment>` | `DoctorPracticeReservations.ViewOwn`; archived rows also `Archives.Reservations.ViewPractice` |
| MP-07 | `/doctors/me/patients/{patientId}/follow-ups` | `ClinicalPage<DoctorPatientFollowUp>` | `FollowUpEligibility.ViewOwn` |
| MP-08 | `/doctors/me/prescriptions/{prescriptionId}/versions/{versionNumber}/pdf` | Private `application/pdf` bytes | `Prescriptions.ViewOwn` AND `Prescriptions.ExportOwn`; exact own source/attendance proof |

Any protected own source at an inactive Practice additionally requires `DoctorPatients.ViewOwn` AND `DoctorPatients.ViewHistoricalOwn`. Original source and archive permissions remain independent. Directory relationship metadata remains visible without the historical permission; inaccessible source sections are unavailable. Directory never confers a mutation, financial right or cross-Doctor medical entitlement.

## Query contracts

MP-01: `search` (max 200), `practiceId`, `lastAttendanceFrom`, `lastAttendanceTo`, `minAttendanceCount` (>0), `hasUpcomingAppointment`, `practiceScope=all|active|historicalOnly`, `pageNumber=1`, `pageSize=20` (max 100), additive `asOfUtc` (UTC timestamp with room for its preceding 30-day summary window; normally reuse the server-returned anchor).

MP-03..06: `practiceId`, `fromDate`, `toDate`, `pageNumber`, `pageSize`; `status` on attendance/prescription/appointment routes. MP-07: `practiceId`, `status`, `pageNumber`, `pageSize`. Invalid ranges, IDs, statuses and pagination return localized validation ProblemDetails. Pages are bounded to 1..1,000,000; page sizes 1..100. All Practice filters must belong to the current Doctor.

Attendance statuses: Waiting, Called, InProgress, Completed, Cancelled, NoShow. These preserve admission evidence; only an actual linked completed MedicalEncounter counts as a completed clinical examination. Diagnosis timeline contains active, non-voided diagnoses only. Prescription `status` filters the root's current issued version, normally Finalized or Voided; Superseded versions are available in existing version reads. A Superseded current-status filter has no matches. Never-issued Draft roots are absent.

Appointments retain Active, Cancelled, NoShow, Expired, ConvertedToTicket. The `source` is the original booking source, `scheduledStart` is the stored UTC appointment instant, and `businessDate` is the original Practice-local date. FollowUp statuses are effective Available, Reserved, Consumed, Expired; expiration uses each Practice's current configured timezone, without mutating the stored row. Each returned eligibility already owns its source entitlement, so `canCreateFollowUpFromSource=false`; Doctor `canBook=false`. Eligible source encounters without an entitlement use the existing Encounter capability and create command.

MP-08: positive `versionNumber`, `disposition=inline|attachment` (default inline). Filename is server-generated ASCII `wasla-prescription-{id:N}-v{n}.pdf`, containing no patient name. Print uses the inline viewer. A missing/draft/unissued version returns safe 404; a status change during rendering returns 409 for a fresh request; unavailable Chromium/rendering returns 503 with a `PrescriptionPdf.*` code. The project-standard 15 MiB PDF output limit applies. Failed authorization or required audit never releases PDF bytes.

## Directory semantics

Default membership is all time, independent of Patient account linkage and terminal Ticket status. Search executes only over the Doctor's qualified patients, using current Arabic/English name, case-insensitive English name/email and phone with formatting removed. No relatives' contact fallback exists.

`practiceId` narrows qualifying attendance and first/last/count/practice statistics. `active` narrows to active-Practice attendance; `historicalOnly` means the patient has no admission at any currently active owned Practice. These filters do not create a new Patient identity. Summary counts apply Practice/scope filters, before search/date/minimum/upcoming/page filters. Returning means at least two scoped admissions.

Ordering is last admission UTC descending, then PatientId. Reuse the first response's `asOfUtc` on adjacent pages to anchor Ticket admission times while new check-ins arrive. This is a paging cutoff, not a clinical/permission cache: current identity, grants and Practice/source state are always revalidated. Refresh the directory after a Practice-scope change. No server-side PHI cache is added.

Date filters are inclusive and compare the last scoped Ticket's stored Practice-local BusinessDate. They are not converted to the server's calendar date. `attendedLast30Days` counts distinct scoped patients whose last check-in falls in `[asOfUtc - 30 days, asOfUtc]`. Age uses the current UTC calendar date.

`completedEncounterCount` requires `MedicalEncounters.ViewOwn`; `hasUpcomingAppointment` requires `DoctorPracticeReservations.ViewOwn`. If the requested attendance scope contains an inaccessible closed Practice, these complete-scope fields are null. A missing permission never becomes a misleading zero/false. Requesting an upcoming filter without reservation permission returns 403. Upcoming means an authorized own Active reservation strictly after server now.

## DTO fields

Every JSON property uses camelCase. `ClinicalPage<T>` is `{items,totalCount,pageNumber,pageSize}`.

`DoctorPatientsPageResponse`: `items`, `totalCount`, `pageNumber`, `pageSize`, `summary:{allTimeUniquePatients,attendedLast30Days,returningPatients}`, `asOfUtc`.

`DoctorPatientListItem`: `patientId`, `nameAr`, nullable `nameEn/phoneNumber/email`, `dateOfBirth`, calculated `age`, `gender`, `firstAttendanceAtUtc`, `lastAttendanceAtUtc`, `lastAttendanceBusinessDate`, `attendanceCount`, nullable `completedEncounterCount`, `practiceCount`, `lastPractice:{id,nameAr,nameEn,isActive}`, nullable `hasUpcomingAppointment`, `historicalOnly`, `capabilities`.

Capabilities: `canViewAttendances`, `canViewEncounters` (Encounter AND Diagnosis read permission), `canViewDiagnoses`, `canViewPrescriptions`, `canExportPrescriptions`, `canViewAppointments`, `canViewFollowUps`, `canViewLabRequests`, `canViewLabResults`, `canViewLabSubmissions`, and equivalent Radiology flags. These are section capabilities; the exact resource is authorized again at every read/download.

`DoctorMyPatientOverviewResponse`: `patient` (the list item), owned attended `practices`, nullable `lastAttendance`, `lastDiagnosis`, `lastPrescription`, permission-filtered nullable issued `labRequestCount/radiologyRequestCount`, nullable earliest `nextAppointment`, nullable latest-created `currentFollowUp`. No clinical notes, relatives, evidence, media keys, patient mutation token or other-Doctor data.

`DoctorPatientAttendance`: `ticketId`, `practiceId`, `practiceName`, `businessDate`, `checkInAtUtc`, `source`, `checkInMode`, `ticketStatus`, `ticketNumber`, `isArchived`, nullable `linkedReservationId/encounterId/encounterStatus`, `canViewTicket`, `canViewEncounter`. Encounter references require both original Encounter and Diagnosis permissions. Sort: check-in descending, TicketId.

`DoctorPatientDiagnosis`: `diagnosisId`, `encounterId`, `practiceId`, `businessDate`, `encounterDate`, `diagnosisType`, `displayText`, `encounterStatus`, `isActive=true`, `isHistoricalPractice`. No private notes/amendment snapshots. Sort: Encounter start descending, DiagnosisId.

`DoctorPatientPrescription`: `prescriptionId`, `encounterId`, `practiceId`, `visitDateUtc`, `currentVersionNumber`, `currentStatus`, `medicationCount`, `hasVersions`, `canView`, `canExport`, `isHistoricalPractice`. Export additionally needs `Prescriptions.ExportOwn`. Sort: visit descending, PrescriptionId.

`DoctorPatientAppointment`: `reservationId`, `practiceId`, `businessDate`, `scheduledStart`, snapshot `visitType/segment`, `status`, nullable `linkedTicketId/followUpEligibilityId`, `isArchived`, booking `source`. Includes standalone and converted bookings once Doctor-wide membership exists. Sort: scheduled UTC descending, ReservationId.

`DoctorPatientFollowUp`: `eligibilityId`, `sourceEncounterId`, `practiceId`, effective `status`, `validUntil`, nullable `reservedReservationId/reservedTicketId/consumedEncounterId`, `canBook`, `canCreateFollowUpFromSource`, `isHistoricalPractice`. Sort: eligibility creation descending, EligibilityId. Reservation and Ticket claims remain mutually exclusive under the original workflow.

## Existing reads adapted; commands preserved

- Own Encounter list/detail, by-Ticket detail and amendments use a named read authorization path. Closed-Practice details report every mutation capability false, suppress drafts and disable visibility changes/FollowUp booking.
- Own prescription detail and versions allow issued historical reads. Closed-Practice correction-draft reads and draft versions are unavailable; all correction/void/draft writer guards remain active-Practice-only.
- Existing Doctor Lab/Radiology request/result/submission lists, details, histories, versions and attachment reads accept authorized own closed-Practice sources. Capabilities are false on inactive-Practice resources. Existing owner-only upload/review/correction/cancellation commands remain guarded, including idempotency replay.
- Existing `/api/v1/practices/{practiceId}/tickets/{ticketId}` supports own historical details, preserves archive rights, disables all operations/refunds and omits financial identifiers. Its existing clinical source rights remain separate.
- Existing `/doctors/me/practices/{practiceId}/reservations/{reservationId}` supports the exact owned historical reservation with all mutation capabilities false. Phase 18's original standalone archived-booking detail remains readable without admission; this source read never creates directory membership. All new patient routes, including MP-06, first require actual attendance. Existing general Practice list/booking availability and writers retain their active-Practice policies; use MP-06 for the patient-wide inactive-Practice appointment list.
- Existing practice-scoped clinical foreign-Practice denials retain their established opaque 403 contract. New unrelated patient/resource routes return safe 404. No old public DTO is replaced.

## Security and integration behavior

All new reads are no-store/nosniff; the Doctor workspace middleware also covers existing Doctor source reads/downloads. Patient reads use the existing private media contracts. No PHI request/response payload logging or shared caching is added. Medical reads and export use append-only metadata-only audits; the new audit category is excluded from ordinary Audit Explorer and requires its existing sensitive-audit authorization.

Directory/tab GETs reuse `medical-record` rate limiting (1,200/15 min per actor); PDF uses `reports-export` (12/15 min per actor) plus bounded renderer concurrency/timeout. Existing 429 ProblemDetails/Retry-After behavior applies.

401: sign in. 403: actor/approval/permission/scope unavailable. 404: unrelated/missing source. 409: refresh state. 422: correct filters. 429: respect Retry-After. 503: PDF runtime unavailable. Never retain PHI after a 403/404, or infer authority from cached IDs/capabilities.

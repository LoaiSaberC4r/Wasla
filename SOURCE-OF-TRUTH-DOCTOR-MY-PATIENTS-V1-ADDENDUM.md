# Doctor My Patients V1 — approved task incorporation

2026-10-28. Branch: `codex/doctor-my-patients-v1`.

The owner supplied and approved `WASLA-CODEX-DOCTOR-MY-PATIENTS-V1.md`, retained verbatim in this checkout. This additive record incorporates that task's decisions. The authoritative Master v1.2 file is absent from this checkout; it was neither read nor edited. No formal Master adoption, legal certification, deployment approval or test completion is asserted here.

| Rule | Status | Implementation boundary |
| --- | --- | --- |
| MP-BR-01/02 | APPROVED IN THIS TASK; existing global identity CONFIRMED | Durable actual Ticket admission, independent of later status, across owned Practices and live/archive storage. One Doctor-wide Patient row; no ownership aggregate. |
| MP-BR-03 | APPROVED IN THIS TASK | Current centralized identity/contact, nullable minor contacts, calculated age; no relatives, evidence, keys or profile writer. |
| MP-BR-04 | CONFIRMED and extended by this task | Actual attendance and clinical Encounter remain distinct; no fabricated encounters. |
| MP-BR-05 | CONFIRMED | Own active diagnosis timeline; full notes/amendments through separately authorized existing readers; completed corrections use existing amendments. |
| MP-BR-06 | APPROVED IN THIS TASK; existing version lifecycle CONFIRMED | Own issued prescriptions, finalized/superseded/voided versions and private version PDF; drafts never exported. |
| MP-BR-07 | CONFIRMED | Phase 16 active treating Ticket + InProgress Encounter + current permission + sharing flag + revision-bound hidden grants unchanged. No historical cross-Doctor scope. Staging/Production sharing disabled. |
| MP-BR-08 | CONFIRMED | Reuse own patient-filtered diagnostic readers and original-owner review; PendingReview is never an official/shared result. |
| MP-BR-09 | CONFIRMED | All own patient reservations once membership exists; explicit completed-source eligibility; Patient/Reception booking; one eligibility per FollowUp. |
| MP-BR-10 | APPROVED IN THIS TASK | Separate read-only closed-Practice decision, current active/first-login-complete Approved Doctor, historical + original source permissions, exact ownership and attendance. Existing writer guards retain active-Practice requirement. |
| MP-BR-11 | APPROVED IN THIS TASK | All-time, descending last check-in/PatientId, scoped search and bounded pagination, permission-filtered optional counts, returning >=2. |
| MP-BR-12 | CONFIRMED | Current DB actor/grants, opaque unrelated IDs, source authorization, private responses, required metadata-only audit, no PHI caching. |

PROPOSED TECHNICAL DESIGN: Application relationship/read policies and CQRS contracts; infrastructure SQL grouping over existing `AllTickets` plus permanent registry identity; existing immutable audit store; existing installed Chromium/Puppeteer renderer pattern; versioned GET controllers; additive lookup indexes only where absent. No new business tables.

Directory metadata is relationship data authorized by `DoctorPatients.ViewOwn`, including inactive-Practice membership. Source details and optional clinical/reservation counts additionally require their original permission and active/historical read scope. Without historical permission an inactive-Practice member remains listed, but protected historical details are unavailable. Archive Ticket/Reservation details retain their archive permission as well as the original source permission.

CONFIRMED compatibility clarification: Phase 18's existing own archived Reservation detail supports standalone bookings without attendance, including closed Practices. That existing source endpoint proves the exact Reservation's Doctor/Practice ownership and retains that read behavior (with the new historical/source grants on the closed path). It does not add a My Patients member. Every new patient route still requires actual Ticket admission first. Existing Reception archive rights are also preserved; the new Doctor-only historical path cannot replace their separate authorization.

Directory `practiceId` narrows qualifying attendance and its first/last/count/practice statistics. `active` selects active-Practice attendance. `historicalOnly` means the patient has no attendance at any currently active owned Practice. The summary applies Practice/scope filters, before search/date/count/upcoming/page filters. Last-30-days is the rolling UTC interval starting server-now minus 30 days. Date filters use the last Ticket's stored Practice-local BusinessDate, not server calendar dates. Age uses the current UTC date. Optional protected fields are null when unavailable; an unauthorized upcoming filter returns 403.

DEFERRED: legally certified e-prescribing/signatures, direct Doctor booking, new writers, notification expansion, clinical retention/disposal and policy changes. Master incorporation requires the missing artifact. These are not implementation claims or legal sign-offs.

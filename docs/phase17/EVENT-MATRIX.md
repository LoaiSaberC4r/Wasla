# Phase 17 event/channel implementation

Every enabled channel is conditional on current source, recipient and channel policy. Clinical email requires explicit opt-in and its independent release gate; clinical In-App requires its independent release gate. Both release gates default false. Legacy booking email ownership remains the default until controlled cutover; its historical recipient behavior is not represented as the new guarded policy.

| Approved event | Status and durable source hook | Email | In-App | Executable evidence |
|---|---|---|---|---|
| Reservation.Created | Implemented; Added Created ReservationHistory | Existing owner by default; guarded owner available through cutover | Eligible recipients | E01, R05 |
| Reservation.Rescheduled | Implemented; actual Rescheduled history and new revision pointer | Same single-owner cutover | Eligible recipients | A08–A12, E01 |
| Reservation.Cancelled | Implemented; Cancelled history, including governance system cancellations | Same single-owner cutover; safe content only | Eligible recipients | A08, E01, E06 |
| Appointment.ReminderDue | Implemented; one event/intent per actual appointment revision | Existing outbox with final guard | Eligible users only | A01–A22, O02–O08 |
| Reservation.RestoredFromNoShow | Implemented; RestoredFromNoShow history | Guarded email; no duplicated reminder | Eligible recipients | A13–A14 |
| Reservation.Expired | Implemented; Expired history from actual source mutation | None | Eligible recipients | E02 |
| Reservation.MarkedNoShow | Implemented; actual MarkedNoShow history | None | Eligible recipients | E02 |
| Reservation.ConvertedToTicket / Patient.CheckedIn | Implemented; one ConvertedToTicket history maps to one canonical Patient.CheckedIn | None | One eligible attendance update | A16, E03 |
| Ticket.Called | Implemented exclusion; no capture | None | None | E04 |
| Ticket.Recalled | Implemented exclusion; no capture | None | None | E04 |
| Doctor.Approved | Implemented; persisted DoctorStatusHistory identity | Existing governance email only | Current eligible target account | E06 |
| Doctor.Rejected | Implemented; persisted DoctorStatusHistory identity | Existing governance email only | Only if currently operable/authorized; otherwise safe durable skip | E06, E07, existing governance regression |
| Doctor.Suspended | Implemented; persisted DoctorStatusHistory identity | Existing governance email only; resulting cancellations captured once | Only if eligible, with no suspension/authentication bypass | E06 |
| Reception.AccountInvitation | Existing email preserved; no new capture | Existing invitation only | None | E07, existing Reception API regression |
| Prescription.Available | Implemented; newly finalized PrescriptionVersion ID, including final correction | Generic, patient-self, opt-in and release gate | Patient-self, source visibility and release gate | C01–C02 |
| Lab.RequestAvailable | Implemented; Published LabRequestHistory, including PostVisit | Same clinical policy | Same clinical policy | C03–C04 |
| Radiology.RequestAvailable | Implemented; Published RadiologyRequestHistory, including PostVisit | Same clinical policy | Same clinical policy | C03–C04 |
| Lab.OfficialResultAvailable | Implemented; Recorded official result history/current version from doctor upload or accept | Same clinical policy; no patient-submission trigger | Same clinical policy | C05–C06 |
| Radiology.OfficialResultAvailable | Implemented; Recorded official result history/current version from doctor upload or accept | Same clinical policy; no patient-submission trigger | Same clinical policy | C05–C06 |
| DiagnosticResult.Corrected | Implemented; Corrected history and current official version, both diagnostic kinds | Generic update under current clinical policy | Same clinical policy | C07 |
| DiagnosticResult.Voided | Implemented; Voided history/version; patient-safe invalidation only | Generic update under current clinical policy | Same clinical policy; no document/body disclosure | C07 |
| Doctor.NewBooking | Implemented; Created reservation history with distinct event identity | None | Current eligible practice owner only | E05, R15 |
| Doctor.BookingCancelled | Implemented; Cancelled reservation history with distinct event identity | None | Current eligible practice owner only | E05, E06 |
| Doctor.DailyScheduleDigest | Implemented exclusion | None | None | E08 |
| Doctor.NoShowDigest | Implemented exclusion | None | None | E08 |

Source capture is in the existing save transaction, before external delivery. No-op/failed mutations have no added history, so they produce no added event or reminder. Uncorrelated security/account and pending historical emails retain their original outbox behavior. No VisitSummary.Available or Prescription.Voided notification was introduced.

The complete per-case test method, outcome and actual TRX reference are in [ACCEPTANCE.md](ACCEPTANCE.md) and [TEST-EVIDENCE.json](TEST-EVIDENCE.json). Implementation of a guarded channel is distinct from independently approved production activation.

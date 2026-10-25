# Phase 17 API and permission inventory

All routes use API version 1.0 and authenticated CurrentUser identity. JSON enum names follow existing API serialization conventions. Existing source endpoint request/response contracts are unchanged.

| Method and full route | Required permission | Request | Response |
|---|---|---|---|
| GET `/api/v1/notifications/mine` | `Notifications.ViewOwn` | `pageNumber=1`, `pageSize=20` (max 100), optional `category`, `isRead` | `NotificationPage<InboxItem>` |
| GET `/api/v1/notifications/mine/unread-count` | `Notifications.ViewOwn` | None | `{ unreadCount }` |
| POST `/api/v1/notifications/mine/{notificationId}/read` | `Notifications.MarkReadOwn` | No identity/body parameters | 204; idempotent read, cross-user/hidden item 404 |
| POST `/api/v1/notifications/mine/read-all` | `Notifications.MarkReadOwn` | No identity/body parameters | 204; one owner-scoped bulk update |
| GET `/api/v1/notifications/me/preferences` | `Notifications.PreferencesViewOwn` | None | `PreferencesResponse` |
| PUT `/api/v1/notifications/me/preferences` | `Notifications.PreferencesManageOwn` | `preferredLanguage`, `preferences[]`, `rowVersion` | Resolved preferences and new version |
| GET `/api/v1/admin/notification-settings` | `Notifications.SettingsView` | None | `NotificationSettingsResponse` |
| PUT `/api/v1/admin/notification-settings` | `Notifications.SettingsManage` | `appointmentReminderEnabled`, `reminderLeadMinutes`, required `reason`, `rowVersion` | Settings and new version; immutable audit |
| GET `/api/v1/admin/notification-deliveries` | `Notifications.DeliveriesView` | Optional `status`, `channel`, `eventType`, `fromUtc`, `toUtc`, pagination | `NotificationPage<DeliveryResponse>` |
| GET `/api/v1/admin/notification-deliveries/{deliveryId}` | `Notifications.DeliveriesView` | None | Metadata-only `DeliveryResponse` |
| POST `/api/v1/admin/notification-deliveries/{deliveryId}/retry` | `Notifications.DeliveriesRetry` | Required `reason`, `rowVersion`; unknown members rejected | 204 on accepted retry; audited current eligibility checks |

Admin routes additionally require SuperAdmin role/current profile. Permissions are checked against current database grants as well as request identity. Suspended/unapproved doctors and inoperable accounts receive no authentication bypass. Reception account eligibility requires a current assignment. Own inbox/preferences permissions do not confer source access.

Successful reads/updates return 200 except the 204 actions above. Existing ProblemDetails conventions remain: unauthenticated 401, permission denied 403, not found/not owned 404, malformed JSON/binding 400, FluentValidation failures **422**, concurrency/invalid retry state 409. The implementation deliberately preserves the repository's 422 validation convention.

`NotificationPage<T>` contains `items`, `pageNumber`, `pageSize`, `totalCount`. Inbox ordering is `createdAtUtc DESC, id DESC`, with count and paging performed in SQL after visibility filtering. An item has `id`, `eventType`, `category`, neutral `title/message`, `createdAtUtc`, `readAtUtc`, `isRead`, and navigation-only `target`. Clinical target has `resourceType=MedicalRecord` and no source key. Reading/marking a notification never reads or mutates its source.

Preferences expose four independent editable category/channel combinations: AppointmentReminder Email/InApp, ClinicalAvailability Email/InApp. Reminders default enabled, clinical email defaults disabled. `preferredLanguage` is `ar` or `en`; template fallback is Arabic. Essential security/account/booking categories cannot be edited through this API. A preference version is a base64 SHA-256 aggregate of the current preference rowversions (32 bytes); send back the exact token. Partial updates preserve unspecified enabled values and update the common language.

Settings expose `appointmentReminderEnabled`, `reminderLeadMinutes`, monotonic `version` and base64 SQL rowversion (8 bytes). Update reason is required, max 500 characters. Settings updates do not rewrite existing intents.

Delivery monitoring includes `id`, `eventType`, `category`, `channel`, `status`, `attemptCount`, `createdAtUtc`, `dispatchedAtUtc`, `lastAttemptAtUtc`, safe `failureCode`, `retryEligible` and `rowVersion`. Email state comes from the correlated outbox. Delivery version is a base64 hash of delivery plus outbox rowversions (32 bytes), so a worker claim invalidates stale retry requests. Retry supports failed correlated email only; sent, processing, queued, suppressed and stale work is rejected. The body has no arbitrary recipient/template/body fields.

## Grants

Patient, Doctor, Reception and SuperAdmin roles receive the four own inbox/preferences permissions. SuperAdmin receives the four settings/monitor/retry permissions. No new permissions are added to Reception's assignment-scoped clinical or financial capabilities. IDs use the existing append-only `SystemPermissionIds.For` convention. Seeding is idempotent and does not remove existing grants or alter Root-only governance.

## Source endpoints with internal integration

| Existing endpoint family | Actual source hook and behavior |
|---|---|
| Patient POST `/api/v1/reservations`; POST `/api/v1/reservations/mine/{id}/cancel` and `/reschedule` | New reservation histories create durable patient/doctor intents; actual appointment revisions evaluate reminder scheduling. Existing booking email owner is preserved by default. |
| Reception POST `/api/v1/reception/practices/{practiceId}/reservations`; reservation `cancel`, `reschedule`, `restore-no-show` | Same capture after existing assignment/booking policies; restoration does not create a new revision or duplicate reminder. |
| Doctor POST `/api/v1/doctors/me/practices/{practiceId}/reservations/{id}/cancel` and `/reschedule` | Same capture after existing owner/consent rules. |
| POST `/api/v1/practices/{practiceId}/reservations/{id}/check-in` and `/force-check-in` | Converted history maps to exactly one Patient.CheckedIn In-App event and invalidates unsent reminders. Ticket/payment remain atomic. |
| Admin doctor `approve`, `reject`, `suspend` | Persisted DoctorStatusHistory capture; existing email retained; eligible In-App only. Suspension cancellation histories are captured once. |
| Ticket complete; prescription correction finalization | Finalized PrescriptionVersion ID captures Prescription.Available; no draft or Prescription.Voided trigger. |
| Lab/Radiology request publication, including PostVisit | Actual Published history captures RequestAvailable; no draft event. |
| Doctor result upload, submission accept, correction, void | Official result histories/current version capture the approved availability/update events. Pending patient submission is excluded. |

Ticket Called/Recalled, digests, marketing, public arbitrary sends, Reception Inbox invitation, dependent clinical notifications, editable HTML templates, SignalR, SMS, WhatsApp and Push are not introduced.

## Exact mutation route inventory

Every route below is POST and starts with `/api/v1/`. The existing handler/service remains responsible for its source mutation; the changed common integration is `WaslaDbContext.SaveChangesAsync`/`NotificationCapture`. No controller signature or source response contract changes. `ReservationApplicationService` and `DoctorLifecycleService` additionally select booking email ownership. Clinical handlers were not edited because their newly persisted histories/versions already reach the common capture boundary.

| Existing route after `/api/v1/` | Source handler/service | Captured events | Channel behavior and compatibility evidence |
|---|---|---|---|
| `reservations` | ReservationApplicationService | Reservation.Created, Doctor.NewBooking, eligible ReminderDue | Single existing/new email owner; eligible In-App. R05/E01 and reservation API regression. |
| `reservations/mine/{reservationId}/cancel` | ReservationApplicationService | Reservation.Cancelled, Doctor.BookingCancelled | Single booking email owner/In-App; old reminder invalidated. A08/E01. |
| `reservations/mine/{reservationId}/reschedule` | ReservationApplicationService | Reservation.Rescheduled, new eligible ReminderDue | Actual revision only; single booking email owner/In-App. A09–A12. |
| `reception/practices/{practiceId}/reservations` | ReservationApplicationService | Reservation.Created, Doctor.NewBooking, eligible ReminderDue | Existing assignment/booking rules; single owner/In-App. R05/E01 and Reception regression. |
| `reception/practices/{practiceId}/reservations/{reservationId}/cancel` | ReservationApplicationService | Reservation.Cancelled, Doctor.BookingCancelled | Single owner/In-App; invalidate reminder. A08/E01. |
| `reception/practices/{practiceId}/reservations/{reservationId}/reschedule` | ReservationApplicationService | Reservation.Rescheduled, eligible ReminderDue | Single owner/In-App; new revision only. A09–A12. |
| `reception/practices/{practiceId}/reservations/{reservationId}/restore-no-show` | ReservationApplicationService | Reservation.RestoredFromNoShow | Guarded restoration email/In-App; no reminder revision/backfill/catch-up. A13/A14. |
| `doctors/me/practices/{practiceId}/reservations/{reservationId}/cancel` | ReservationApplicationService | Reservation.Cancelled, Doctor.BookingCancelled | Existing owner/consent policy; single owner/In-App. E01 and operational practice regression. |
| `doctors/me/practices/{practiceId}/reservations/{reservationId}/reschedule` | ReservationApplicationService | Reservation.Rescheduled, eligible ReminderDue | Existing owner/consent policy; new revision only. A09–A12 and operational practice regression. |
| `practices/{practiceId}/reservations/{reservationId}/check-in` | Check-in command handler | Patient.CheckedIn | In-App only, canonical ConvertedToTicket history; no duplicate event. A16/E03. |
| `practices/{practiceId}/reservations/{reservationId}/force-check-in` | Force-check-in command handler | Patient.CheckedIn | Same canonical event, unchanged ticket/payment transaction. A16/E03. |
| `admin/doctors/{doctorId}/approve` | DoctorLifecycleService | Doctor.Approved | Existing governance email; currently eligible In-App. E06 and governance regression. |
| `admin/doctors/{doctorId}/reject` | DoctorLifecycleService | Doctor.Rejected | Existing governance email; no authentication bypass for In-App. E06/E07 and governance regression. |
| `admin/doctors/{doctorId}/suspend` | DoctorLifecycleService | Doctor.Suspended and actual cancellation events | Governance email retained, cancellations have one booking email owner. E06. |
| `practices/{practiceId}/tickets/{ticketId}/complete` | CompleteTicketCommandHandler | Finalized Prescription.Available and published Lab/Radiology.RequestAvailable when present | Generic patient-self clinical channels, independent gates, no draft delivery. C01–C04 and ticket/clinical regression. |
| `doctors/me/prescriptions/{prescriptionId}/correction-draft/finalize` | MutatePrescriptionHandler | Prescription.Available for new finalized version | Same clinical policy; no correction-draft event. C02. |
| `doctors/me/practices/{practiceId}/encounters/{encounterId}/lab-requests/post-visit` | DiagnosticOrderHandler | Lab.RequestAvailable | Published source only; generic gated channels. C03/C04. |
| `doctors/me/practices/{practiceId}/encounters/{encounterId}/radiology-requests/post-visit` | DiagnosticOrderHandler | Radiology.RequestAvailable | Published source only; generic gated channels. C03/C04. |
| `doctors/me/lab-requests/{requestId}/results` | DiagnosticResultHandler | Lab.OfficialResultAvailable | Official upload only; generic gated channels. C05. |
| `doctors/me/radiology-requests/{requestId}/results` | DiagnosticResultHandler | Radiology.OfficialResultAvailable | Official upload only; generic gated channels. C05. |
| `doctors/me/lab-result-submissions/{submissionId}/accept` | DiagnosticResultHandler | Lab.OfficialResultAvailable | Acceptance creates official result; pending submission has no notification. C06. |
| `doctors/me/radiology-result-submissions/{submissionId}/accept` | DiagnosticResultHandler | Radiology.OfficialResultAvailable | Same acceptance policy. C06. |
| `doctors/me/lab-results/{resultId}/corrections` | DiagnosticResultHandler | DiagnosticResult.Corrected | Current official version, generic update only. C07. |
| `doctors/me/radiology-results/{resultId}/corrections` | DiagnosticResultHandler | DiagnosticResult.Corrected | Same current-version policy. C07. |
| `doctors/me/lab-results/{resultId}/void` | DiagnosticResultHandler | DiagnosticResult.Voided | Generic invalidation; no clinical payload/document disclosure. C07. |
| `doctors/me/radiology-results/{resultId}/void` | DiagnosticResultHandler | DiagnosticResult.Voided | Same invalidation policy. C07. |

Reservation NoShow/expiry and system cancellation workers share this capture integration without a new HTTP route. Existing security/account verification routes additionally preserve newly consumed, exactly bound contact evidence through `NotificationContactCapture`; their emails, OTP behavior and public contracts remain unchanged. Archive readers do not recapture source events. The actual executable evidence for the references above is in [ACCEPTANCE.md](ACCEPTANCE.md).

The contact-evidence integration applies to POST `/api/v1/patient-onboarding/register`, the compatibility POST `/api/v1/auth/patients/register`, POST `/api/v1/patient-onboarding/activate`, and PUT `/api/v1/patients/me` when it consumes a valid email-change proof. Their existing identity workflows and proof requirements remain authoritative; these saves add fingerprint evidence, not a notification event or an additional security email. OTP start/verification alone cannot create trusted recipient identity.

# Existing APIs reused by Part One

These existing routes retain their original semantics, authorization, DTOs, status codes, idempotency/version requirements and clinical/financial safeguards. No competing command is introduced. This generated inventory combines exact runtime routes with current role/policy metadata; the existing phase documentation remains the business authority. The [reused OpenAPI snapshot](part1-reused-openapi.json) supplies every field, enum, nullable member and response for these commands.

Patient composition invokes self handlers, explicitly resolves self PatientId and keeps family/attachment/medical policies. Reception actions revalidate actual active assignment plus delegated underlying permission. Doctor handover/investigation links do not elevate access to these resources. Public routes still have their existing public semantics; this inventory does not grant private access.

| Method / route | Controller.action | Roles | Permission policy | Classification |
|---|---|---|---|---|
| `GET /api/v1/admin/reservations` | AdministrativeReservations.List | Existing authorization/scope policy; no role shortcut | Permission:Reservations.ViewAdministrative | REUSED |
| `GET /api/v1/admin/reservations/{reservationId}` | AdministrativeReservations.Get | Existing authorization/scope policy; no role shortcut | Permission:Reservations.ViewAdministrative | REUSED |
| `GET /api/v1/doctors/me/financial-transactions` | DoctorFinanceRead.Transactions | Doctor | Permission:DoctorPracticePayments.ViewOwn | REUSED |
| `GET /api/v1/doctors/me/practices` | DoctorPractices.List | Existing authorization/scope policy; no role shortcut | Permission:DoctorPractices.ViewOwn | REUSED |
| `POST /api/v1/doctors/me/practices` | DoctorPractices.Create | Existing authorization/scope policy; no role shortcut | Permission:DoctorPractices.ManageOwn | REUSED |
| `GET /api/v1/doctors/me/practices/{practiceId}/reservations` | DoctorReservations.List | Existing authorization/scope policy; no role shortcut | Permission:DoctorPracticeReservations.ViewOwn | REUSED |
| `GET /api/v1/doctors/me/practices/{practiceId}/reservations/filter-options` | DoctorReservations.FilterOptions | Existing authorization/scope policy; no role shortcut | Permission:DoctorPracticeReservations.ViewOwn | REUSED |
| `GET /api/v1/doctors/me/practices/{practiceId}/reservations/{reservationId}` | DoctorReservations.Get | Existing authorization/scope policy; no role shortcut | Permission:DoctorPracticeReservations.ViewOwn | REUSED |
| `POST /api/v1/doctors/me/practices/{practiceId}/reservations/{reservationId}/cancel` | DoctorReservations.Cancel | Existing authorization/scope policy; no role shortcut | Permission:DoctorPracticeReservations.CancelOwn | REUSED |
| `POST /api/v1/doctors/me/practices/{practiceId}/reservations/{reservationId}/reschedule` | DoctorReservations.Reschedule | Existing authorization/scope policy; no role shortcut | Permission:DoctorPracticeReservations.RescheduleOwn | REUSED |
| `GET /api/v1/doctors/me/practices/{practiceId}/reservations/{reservationId}/reschedule/available-dates` | DoctorReservations.RescheduleDates | Existing authorization/scope policy; no role shortcut | Permission:DoctorPracticeReservations.RescheduleOwn | REUSED |
| `GET /api/v1/doctors/me/practices/{practiceId}/reservations/{reservationId}/reschedule/available-slots` | DoctorReservations.RescheduleSlots | Existing authorization/scope policy; no role shortcut | Permission:DoctorPracticeReservations.RescheduleOwn | REUSED |
| `GET /api/v1/doctors/me/practices/{practiceId}/tickets/{ticketId}/encounter` | DoctorEncounters.ByTicket | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/doctors/me/practices/{practiceId}/tickets/{ticketId}/medical-record/access-requests` | SentMedicalRecordAccessRequests.List | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `POST /api/v1/doctors/me/practices/{practiceId}/tickets/{ticketId}/medical-record/access-requests` | SentMedicalRecordAccessRequests.Create | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/doctors/me/practices/{practiceId}/tickets/{ticketId}/medical-record/access-requests/{requestId}` | SentMedicalRecordAccessRequests.Details | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/doctors/me/practices/{practiceId}/tickets/{ticketId}/medical-record/encounters/{encounterId}` | TreatingMedicalRecord.Encounter | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/doctors/me/practices/{practiceId}/tickets/{ticketId}/medical-record/items/{resourceType}/{sourceId}` | TreatingMedicalRecord.Item | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/doctors/me/practices/{practiceId}/tickets/{ticketId}/medical-record/items/{resourceType}/{sourceId}/attachments/{attachmentId}/content` | TreatingMedicalRecord.Content | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/doctors/me/practices/{practiceId}/tickets/{ticketId}/medical-record/overview` | TreatingMedicalRecord.Overview | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/doctors/me/practices/{practiceId}/tickets/{ticketId}/medical-record/timeline` | TreatingMedicalRecord.Timeline | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/doctors/me/revenue/dashboard` | DoctorFinanceRead.Dashboard | Doctor | Permission:DoctorRevenue.ViewOwn | REUSED |
| `GET /api/v1/families/mine` | Families.Mine | Existing authorization/scope policy; no role shortcut | Permission:Families.ViewOwn | REUSED |
| `GET /api/v1/families/mine/dependents/{childPatientId}/medical-access` | FamilyMedicalStatus.Status | Existing authorization/scope policy; no role shortcut | Permission:Families.ViewOwn | REUSED |
| `GET /api/v1/families/mine/dependents/{dependentPatientId}/medical-record/encounters/{encounterId}` | DependentMedicalRecord.Encounter | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/families/mine/dependents/{dependentPatientId}/medical-record/items/{resourceType}/{sourceId}` | DependentMedicalRecord.Item | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/families/mine/dependents/{dependentPatientId}/medical-record/items/{resourceType}/{sourceId}/attachments/{attachmentId}/content` | DependentMedicalRecord.Content | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/families/mine/dependents/{dependentPatientId}/medical-record/overview` | DependentMedicalRecord.Overview | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/families/mine/dependents/{dependentPatientId}/medical-record/timeline` | DependentMedicalRecord.Timeline | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `POST /api/v1/families/mine/medical-access-requests` | FamilyMedicalAccessRequests.Create | Existing authorization/scope policy; no role shortcut | Permission:FamilyMedicalAccessRequests.CreateOwn | REUSED |
| `GET /api/v1/families/mine/medical-access-requests/mine` | FamilyMedicalAccessRequests.Mine | Existing authorization/scope policy; no role shortcut | Permission:FamilyMedicalAccessRequests.ViewOwn | REUSED |
| `GET /api/v1/families/mine/medical-access-requests/{requestId}` | FamilyMedicalAccessRequests.Detail | Existing authorization/scope policy; no role shortcut | Permission:FamilyMedicalAccessRequests.ViewOwn | REUSED |
| `GET /api/v1/families/mine/medical-access-requests/{requestId}/documents/{documentId}/content` | FamilyMedicalAccessRequests.Document | Existing authorization/scope policy; no role shortcut | Permission:FamilyMedicalAccessRequests.ViewOwnDocuments | REUSED |
| `GET /api/v1/families/mine/medical-access-requests/{requestId}/history` | FamilyMedicalAccessRequests.History | Existing authorization/scope policy; no role shortcut | Permission:FamilyMedicalAccessRequests.ViewOwn | REUSED |
| `POST /api/v1/families/mine/medical-access-requests/{requestId}/resubmit` | FamilyMedicalAccessRequests.Resubmit | Existing authorization/scope policy; no role shortcut | Permission:FamilyMedicalAccessRequests.ResubmitOwn | REUSED |
| `POST /api/v1/families/mine/medical-access-requests/{requestId}/withdraw` | FamilyMedicalAccessRequests.Withdraw | Existing authorization/scope policy; no role shortcut | Permission:FamilyMedicalAccessRequests.WithdrawOwn | REUSED |
| `GET /api/v1/families/mine/medical-record-dependents` | MedicalRecordDependents.List | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/follow-up-eligibilities/mine` | MyFollowUpEligibilities.List | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/follow-up-eligibilities/mine/{eligibilityId}` | MyFollowUpEligibilities.Details | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/lab-requests/mine` | PatientLab.Requests | Patient | Existing endpoint policy | REUSED |
| `GET /api/v1/lab-requests/mine/{requestId}` | PatientLab.RequestDetails | Patient | Existing endpoint policy | REUSED |
| `GET /api/v1/lab-requests/mine/{requestId}/submissions` | PatientLab.Submissions | Patient | Existing endpoint policy | REUSED |
| `POST /api/v1/lab-requests/mine/{requestId}/submissions` | PatientLab.Submit | Patient | Existing endpoint policy | REUSED |
| `GET /api/v1/lab-results/mine` | PatientLab.Results | Patient | Existing endpoint policy | REUSED |
| `GET /api/v1/lab-results/mine/{resultId}` | PatientLab.Result | Patient | Existing endpoint policy | REUSED |
| `GET /api/v1/lab-results/mine/{resultId}/attachments/{attachmentId}/content` | PatientLab.ResultContent | Patient | Existing endpoint policy | REUSED |
| `GET /api/v1/patients/me` | Patients.Me | Existing authorization/scope policy; no role shortcut | Permission:PatientProfile.ViewOwn | REUSED |
| `PUT /api/v1/patients/me` | Patients.UpdateMe | Existing authorization/scope policy; no role shortcut | Permission:PatientProfile.UpdateOwn | REUSED |
| `GET /api/v1/patients/me/contacts` | Patients.Contacts | Existing authorization/scope policy; no role shortcut | Permission:PatientContacts.ViewOwn | REUSED |
| `POST /api/v1/patients/me/contacts` | Patients.AddContact | Existing authorization/scope policy; no role shortcut | Permission:PatientContacts.ManageOwn | REUSED |
| `DELETE /api/v1/patients/me/contacts/{contactId}` | Patients.DeleteContact | Existing authorization/scope policy; no role shortcut | Permission:PatientContacts.ManageOwn | REUSED |
| `PUT /api/v1/patients/me/contacts/{contactId}` | Patients.UpdateContact | Existing authorization/scope policy; no role shortcut | Permission:PatientContacts.ManageOwn | REUSED |
| `GET /api/v1/patients/me/financial-transactions` | PatientFinanceRead.Transactions | Patient | Permission:Payments.ViewOwn | REUSED |
| `GET /api/v1/patients/me/medical-record/access-log` | PatientMedicalRecord.AccessLog | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/patients/me/medical-record/encounters/{encounterId}` | PatientMedicalRecord.Encounter | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/patients/me/medical-record/items/{resourceType}/{sourceId}` | PatientMedicalRecord.Item | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/patients/me/medical-record/items/{resourceType}/{sourceId}/attachments/{attachmentId}/content` | PatientMedicalRecord.Content | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/patients/me/medical-record/overview` | PatientMedicalRecord.Overview | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/patients/me/medical-record/timeline` | PatientMedicalRecord.Timeline | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/patients/me/payments/{paymentId}/receipt` | PatientFinanceRead.PaymentReceipt | Patient | Permission:Payments.ViewOwn | REUSED |
| `GET /api/v1/patients/me/profile-image` | Patients.ProfileImage | Existing authorization/scope policy; no role shortcut | Permission:PatientProfile.ViewOwn | REUSED |
| `GET /api/v1/patients/me/refunds/{refundId}/receipt` | PatientFinanceRead.RefundReceipt | Patient | Permission:Payments.ViewOwn | REUSED |
| `GET /api/v1/practices/{practiceId}/financial-transactions` | PracticeFinanceRead.Transactions | Doctor,Reception | Existing endpoint policy | REUSED |
| `GET /api/v1/practices/{practiceId}/queue` | PracticeTickets.Queue | Doctor,Reception | Existing endpoint policy | REUSED |
| `POST /api/v1/practices/{practiceId}/queue/call-next` | PracticeTickets.CallNext | Doctor,Reception | Existing endpoint policy | REUSED |
| `POST /api/v1/practices/{practiceId}/reservations/{reservationId}/check-in` | PracticeTickets.CheckIn | Doctor,Reception | Existing endpoint policy | REUSED |
| `POST /api/v1/practices/{practiceId}/reservations/{reservationId}/force-check-in` | PracticeTickets.ForceCheckIn | Doctor,Reception | Existing endpoint policy | REUSED |
| `POST /api/v1/practices/{practiceId}/tickets/walk-in` | PracticeTickets.WalkIn | Doctor,Reception | Existing endpoint policy | REUSED |
| `GET /api/v1/practices/{practiceId}/tickets/{ticketId}` | PracticeTickets.Details | Doctor,Reception | Existing endpoint policy | REUSED |
| `POST /api/v1/practices/{practiceId}/tickets/{ticketId}/cancel` | PracticeTickets.Cancel | Doctor,Reception | Existing endpoint policy | REUSED |
| `POST /api/v1/practices/{practiceId}/tickets/{ticketId}/complete` | PracticeTickets.Complete | Doctor,Reception | Existing endpoint policy | REUSED |
| `POST /api/v1/practices/{practiceId}/tickets/{ticketId}/confirm-no-response` | PracticeTickets.ConfirmNoResponse | Doctor,Reception | Existing endpoint policy | REUSED |
| `POST /api/v1/practices/{practiceId}/tickets/{ticketId}/manual-call` | PracticeTickets.ManualCall | Doctor,Reception | Existing endpoint policy | REUSED |
| `POST /api/v1/practices/{practiceId}/tickets/{ticketId}/recall` | PracticeTickets.Recall | Doctor,Reception | Existing endpoint policy | REUSED |
| `POST /api/v1/practices/{practiceId}/tickets/{ticketId}/restore-no-show` | PracticeTickets.RestoreNoShow | Doctor,Reception | Existing endpoint policy | REUSED |
| `POST /api/v1/practices/{practiceId}/tickets/{ticketId}/start` | PracticeTickets.Start | Doctor,Reception | Existing endpoint policy | REUSED |
| `GET /api/v1/prescriptions/mine` | MyPrescriptions.List | Patient | Existing endpoint policy | REUSED |
| `GET /api/v1/prescriptions/mine/{prescriptionId}` | MyPrescriptions.Get | Patient | Existing endpoint policy | REUSED |
| `GET /api/v1/radiology-requests/mine` | PatientRadiology.Requests | Patient | Existing endpoint policy | REUSED |
| `GET /api/v1/radiology-requests/mine/{requestId}` | PatientRadiology.RequestDetails | Patient | Existing endpoint policy | REUSED |
| `GET /api/v1/radiology-requests/mine/{requestId}/submissions` | PatientRadiology.Submissions | Patient | Existing endpoint policy | REUSED |
| `POST /api/v1/radiology-requests/mine/{requestId}/submissions` | PatientRadiology.Submit | Patient | Existing endpoint policy | REUSED |
| `GET /api/v1/radiology-results/mine` | PatientRadiology.Results | Patient | Existing endpoint policy | REUSED |
| `GET /api/v1/radiology-results/mine/{resultId}` | PatientRadiology.Result | Patient | Existing endpoint policy | REUSED |
| `GET /api/v1/radiology-results/mine/{resultId}/attachments/{attachmentId}/content` | PatientRadiology.ResultContent | Patient | Existing endpoint policy | REUSED |
| `GET /api/v1/reception/practices/{practiceId}/reservations` | ReceptionReservations.List | Reception | Existing endpoint policy | REUSED |
| `POST /api/v1/reception/practices/{practiceId}/reservations` | ReceptionReservations.Create | Reception | Existing endpoint policy | REUSED |
| `GET /api/v1/reception/practices/{practiceId}/reservations/filter-options` | ReceptionReservations.FilterOptions | Reception | Existing endpoint policy | REUSED |
| `GET /api/v1/reception/practices/{practiceId}/reservations/{reservationId}` | ReceptionReservations.Get | Reception | Existing endpoint policy | REUSED |
| `POST /api/v1/reception/practices/{practiceId}/reservations/{reservationId}/cancel` | ReceptionReservations.Cancel | Reception | Existing endpoint policy | REUSED |
| `POST /api/v1/reception/practices/{practiceId}/reservations/{reservationId}/reschedule` | ReceptionReservations.Reschedule | Reception | Existing endpoint policy | REUSED |
| `GET /api/v1/reception/practices/{practiceId}/reservations/{reservationId}/reschedule/available-dates` | ReceptionReservations.RescheduleDates | Reception | Existing endpoint policy | REUSED |
| `GET /api/v1/reception/practices/{practiceId}/reservations/{reservationId}/reschedule/available-slots` | ReceptionReservations.RescheduleSlots | Reception | Existing endpoint policy | REUSED |
| `POST /api/v1/reception/practices/{practiceId}/reservations/{reservationId}/restore-no-show` | ReceptionReservations.RestoreNoShow | Reception | Existing endpoint policy | REUSED |
| `POST /api/v1/reservations` | Reservations.Create | Patient | Existing endpoint policy | REUSED |
| `GET /api/v1/reservations/bookable-patients` | Reservations.BookablePatients | Patient | Existing endpoint policy | REUSED |
| `GET /api/v1/reservations/follow-up-eligibilities/{eligibilityId}/available-dates` | FollowUpBooking.Dates | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/reservations/follow-up-eligibilities/{eligibilityId}/available-slots` | FollowUpBooking.Slots | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/reservations/follow-up-eligibilities/{eligibilityId}/booking-options` | FollowUpBooking.Options | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/reservations/metadata` | ReservationMetadata.Get | Existing authorization/scope policy; no role shortcut | Existing endpoint policy | REUSED |
| `GET /api/v1/reservations/mine` | Reservations.Mine | Patient | Existing endpoint policy | REUSED |
| `GET /api/v1/reservations/mine/{reservationId}` | Reservations.GetMine | Patient | Existing endpoint policy | REUSED |
| `POST /api/v1/reservations/mine/{reservationId}/cancel` | Reservations.CancelMine | Patient | Existing endpoint policy | REUSED |
| `POST /api/v1/reservations/mine/{reservationId}/reschedule` | Reservations.RescheduleMine | Patient | Existing endpoint policy | REUSED |
| `GET /api/v1/reservations/mine/{reservationId}/reschedule/available-dates` | Reservations.MineRescheduleDates | Patient | Existing endpoint policy | REUSED |
| `GET /api/v1/reservations/mine/{reservationId}/reschedule/available-slots` | Reservations.MineRescheduleSlots | Patient | Existing endpoint policy | REUSED |
| `GET /api/v1/tickets/mine/active` | MyTickets.Active | Patient | Existing endpoint policy | REUSED |
| `GET /api/v1/tickets/mine/{ticketId}` | MyTickets.Details | Patient | Existing endpoint policy | REUSED |

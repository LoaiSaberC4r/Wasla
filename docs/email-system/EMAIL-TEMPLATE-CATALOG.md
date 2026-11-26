# Email template catalog

Bilingual Arabic-first delivery is the default for Authentication, Patient Identity, Doctors, Family, Reception, legacy Reservations, and Subscriptions. Guarded reservation and clinical delivery retains the recipient's Arabic or English preference and existing Arabic fallback.

| ID | Key | Dynamic fields | Existing sender or event |
|---|---|---|---|
| 01 | `Authentication.PasswordResetOtp` | OTP code, expiration minutes | Password recovery |
| 02 | `Patient.EmailVerificationOtp` | OTP code, expiration minutes | Patient Identity email verification |
| 03 | `Patient.AccountActivation` | Activation token, expiration hours | Existing activation invitation and resend paths |
| 04 | `Doctor.RegistrationReceived` | Doctor name, registration timestamp | Doctor registration |
| 05 | `Doctor.AccountApproved` | Doctor name, approval timestamp | Doctor governance |
| 06 | `Doctor.RegistrationRejected` | Doctor name, review timestamp, permitted rejection reason | Doctor governance |
| 07 | `Doctor.AccountSuspended` | Doctor name, suspension timestamp, permitted suspension reason | Doctor governance |
| 08 | `Doctor.AccountReactivated` | Doctor name, reactivation timestamp | Doctor governance |
| 09 | `Doctor.SpecializationChangesRequested` | Doctor name, revision, permitted review message | Doctor specialization review |
| 10 | `Patient.IdentityClaimUpdate` | Language only | Existing patient claim update notifications |
| 11 | `Family.RelationshipApproved` | Submitter name, revision | Family relationship review |
| 12 | `Family.RelationshipRejected` | Submitter name, revision | Family relationship review |
| 13 | `Family.RelationshipChangesRequested` | Submitter name, revision | Family relationship review |
| 14 | `Reception.AccountCreated` | Reception name, username | New reception user creation |
| 15 | `Reservation.Confirmed` | Reservation reference, appointment local date and time | Legacy and guarded reservation creation |
| 16 | `Reservation.Rescheduled` | Reservation reference, revised appointment local date and time | Legacy and guarded rescheduling |
| 17 | `Reservation.Cancelled` | Reservation reference, optional appointment local date and time | Legacy and guarded cancellation, including doctor suspension |
| 18 | `Reservation.AppointmentReminder` | Reservation reference, appointment local date and time | Guarded reminder |
| 19 | `Reservation.RestoredFromNoShow` | Reservation reference, appointment local date and time | Guarded restore from no-show |
| 20 | `Clinical.MedicalUpdateAvailable` | Language only | Seven guarded clinical events |
| 21 | `Subscription.TrialStarted` | Doctor name, plan, start and end, practice limit, reception seat limit | Trial start |
| 22 | `Subscription.TrialReminder` | Doctor name, plan, end | Trial reminder |
| 23 | `Subscription.TrialExpired` | Doctor name, plan, end | Trial expiry |
| 24 | `Subscription.TrialExtended` | Doctor name, plan, new end | Trial extension |
| 25 | `Subscription.PaidActivated` | Doctor name, plan, start and end, practice limit, reception seat limit, linked practices | Existing paid activation and applicable upgrade paths |
| 26 | `Subscription.PaidReminder` | Doctor name, plan, end, linked practices, optional catalog renewal price and currency | Existing paid reminders |
| 27 | `Subscription.Expired` | Doctor name, plan, end | Existing paid and legacy subscription expiry paths |

The generic clinical template serves `Prescription.Available`, `Lab.RequestAvailable`, `Radiology.RequestAvailable`, `Lab.OfficialResultAvailable`, `Radiology.OfficialResultAvailable`, `DiagnosticResult.Corrected`, and `DiagnosticResult.Voided`. These notifications carry no medical payload. The guarded clinical sender continues to enforce the patient-self recipient rule, verified email, opt-in, release gate, and authorization.

Optional links are sourced from branding. Reception never passes the temporary password. Family models cannot accept private review notes or family member identities. Patient identity claim updates carry no review decision or claimant data. Subscription contract and period identifiers stay in the existing idempotency and persistence paths, outside customer-facing content.

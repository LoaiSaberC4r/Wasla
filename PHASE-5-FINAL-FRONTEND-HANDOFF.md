# Phase 5 final frontend handoff

Use the E01–E06/N01–N20 numbering and payload/curl reference in [API changes](PHASE-5-FINAL-API-CHANGES.md). No Jira synchronization was performed. API prefix remains `/api/v1`.

## Reception

Choose Basic or Full in an active assigned practice. Before creating, call N02 Search using exact email, phone>=7 digits, or name>=3+DOB. Show only the returned minimal masked candidates. No unfiltered global directory exists. Exact email collision never permits another Patient: choose Use Existing Patient, verify in person, call N02 ConfirmReuse, then pass the original PatientId and opaque patientIdentityAttestationId to the existing booking/Walk-In API. Do not display another doctor's records. Attestation lasts15m, is bound to current staff/practice/patient and is consumed/associated once. Refresh confirmation after expiry or changing practice/staff.

The existing reservation path is `/reception/practices/{practiceId}/reservations`; the existing Walk-In path is **`/practices/{practiceId}/tickets/walk-in`**. The owner confirmed the task's alternate Walk-In reference was an error. No route alias/move exists. Same-practice established scheduling relationships retain prior booking semantics, quotas, pricing and payment/state rules.

Basic E02 needs Arabic name, DOB, gender, required practice and patient phone OR a usable primary contact phone. NameEn/email are optional. Guardian email belongs in primaryContactEmail, never copied into every child's Patient identity email. Basic creates no account. Full N01 is adult-only and needs identity email, verifiedInPerson=true and GovernmentIdSeen/ApprovedAlternateEvidence; do not render any staff password field. Show PendingActivation, masked destination and invitation expiry. Patient chooses password privately from email. Existing Basic email -> ClaimRequired; do not offer Full as an upgrade shortcut. Different-email soft candidates allow creation only after explicit DistinctPerson outcome, verification basis and a recorded reason. No automatic match binding or merge button.

## Patient onboarding

1. N03 Start with Register or Claim (optional patientReference for Claim). Always show the neutral202 receipt; do not infer eligibility/account existence. Respect cooldown60s.
2. N04 Verify six-digit email code. Show generic invalid/expired feedback, max five attempts, Retry-After where supplied. Successful proof lasts30m and must stay out of URLs, analytics and logs.
3. Follow server nextAction exactly: RegisterNew -> N05 adult form; ClaimExisting -> N06 proof form; LoginOrRecovery -> existing Login/Recovery; IdentityConflict -> separate operational account/email guidance; EmailChange -> resume E05 for the authenticated Patient. A Register challenge can produce ClaimExisting without another OTP. Claim without exact match/reference still receives Claim-scoped proof; N06 alone determines plausible intake/evidence eligibility. A reference identifies a candidate, never proves ownership.
4. N05 and legacy E01 require the same emailVerificationToken + UUID Idempotency-Key. Registration returns IDs/status only; perform normal Login separately. Old unverified E01 forms now fail409 VerificationRequired and create nothing.
5. N06 requires independent official identity evidence or a valid48h ClaimProof attestation from Reception N02. Evidence types must correspond to uploaded fields. Adult notes/reference alone fail. Age16–17 also requires DOB/identity evidence and manual SuperAdmin review of an existing Basic; under16 cannot receive a standalone account. A parent's verified contact email grants no child identity/record authority.
6. Store the sanitized Claim ID. N07 needs X-Onboarding-Token for the same email; read remains possible after mutation consumption until proof expiry. Then repeat N03/N04. Never fetch candidate/full Patient profiles as an applicant. Pending/ModificationRequested/Rejected/ApprovedPendingActivation/Linked/Expired are displayed from server state. Status reasons only explain applicant-facing rejection/modification.
7. ModificationRequested -> obtain fresh unused Claim proof -> N08 with current rowVersion, new evidence and optional reason/profile corrections. New immutable revision returns PendingReview. A consumed mutation token cannot submit another revision. Rejected/Expired/Linked do not reopen.
8. ApprovedPendingActivation means no login/medical access yet. N09 accepts only emailed invitation and strong matching password fields. No target/email/UserType selector. Success requires normal Login; same original PatientId is preserved. N10 resend returns generic202, revokes the previous token and gives another48h invitation; Claim resend requires same-email verified proof. Do not recreate Patient after expiry.

## Review screens

Granular authorized SuperAdmins use N11/N12 minimal identity inbox/details and N13 private no-store evidence streaming. No diagnosis/history shortcut exists. Approval requires current-revision reviewedEvidenceIds, explicit targetPatientId and rowVersion; differing applicant reference needs a documented reason. Non-null Patient.Email mismatch blocks approval. Approve -> invitation only; Reject/RequestModification need reason1..1000. Show409 stale versions by refreshing current details, never blindly reapplying an old decision. N17 is immutable identity decision history. Ordinary Reception/Doctor/Patient cannot review.

N18–N20 classification displays masked identities and safe indicators. NeedsMoreEvidence records a review note; ConfirmedDuplicate/DistinctPatients finalize. ConfirmedDuplicate does not combine records or change permissions. No medical merge workflow is implemented.

## Profile and compatibility

E04 preserves own profile properties and adds accountStatus/hasVerifiedIdentityEmail/identityEmailMasked. E05 retains email for compatibility: unchanged normalized email needs no proof, changed email needs authenticated EmailChange N03/N04 with exact target email. Patient and login emails change atomically.409 conflicts leave both old values. Cannot clear a linked identity email. Keep existing rowVersion/image/contact-minimum behavior. E06 is additive; immutable user ID and current access remain authoritative, not a stale JWT email.

E03 replaces raw phoneNumber with phoneNumberMasked and adds emailMasked; no guardian email is returned. Use bounded predicates and pageSize1..100; N02 candidates max20. Use DateOnlyYYYY-MM-DD, UTC Z timestamps, string enums and Base64 opaque rowversions. Idempotency UUID headers are mandatory on new writes except Start/Verify/Activate; preserve a key/body across retries. Existing profile/booking compatibility remains as documented. Never log OTP/proof/invitation/password/private files or put proof in query strings.

## Error/translation behavior

Arabic messages and English resources accompany stable codes; branch on code, not translated text.400 correct input;401 login;403 unavailable permission/scope;404 unavailable status/file without exposing existence;409 collision/state/stale proof/version;422 age or insufficient proof;429 honor Retry-After. ClaimRequired/AlreadyExists lead to Claim or staff reuse as appropriate. Identity.EmailOwnedByAnotherAccount never offers context switching. IdentityAttestationRequired/Invalid returns to staff verification. EmailVerificationRequired returns to EmailChange. VerificationRequired/TokenInvalidOrExpired restarts the appropriate OTP session. Activation.InvalidOrExpired offers controlled resend. Minor restrictions explain Basic/manual supervised Claim where16–17, never silently create an adult-like account.

Retention can expire inactive requests90d; evidence is retained90d after terminal closure and audit24months unless legal hold. Sensitive purge flags remain off by default. Production legal/privacy signoff is a release gate. Frontend implementation must preserve existing Family/guardian age16 clinical policy and fail-closed access independently of Claim status.

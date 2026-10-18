# Phase 5 final API changes

Approval: 2026-10-18. Base path `/api/v1`. This is the implementation handoff; unverified acceptance items remain in the closure report. Endpoint numbers preserve Jira synchronization order; no Jira was written.

## Route and handler inventory

All identity command/query operations use `PatientIdentityWorkflow` through MediatR, FluentValidation and the existing Wasla transaction behavior. Older Basic/profile commands retain their existing handlers. This follows the existing product-specific CQRS envelope pattern used in later Wasla phases. No API controller calls EF or infrastructure implementations.

| ID | Method / path | Permission / proof | Handler | Success | Coverage |

|---|---|---|---|---|---|

| E01 | POST `/api/v1/auth/patients/register` | anonymous verified email | `RegisterPatientCommand -> PatientIdentityCommand(Register)` | 201 | Registration |

| E02 | POST `/api/v1/patients` | Patients.Register + active Reception assignment | `CreateReceptionPatientCommand` | 201 | Registration / SQL |

| E03 | GET `/api/v1/patients/search` | Patients.SearchBasic + active Reception assignment | `SearchPatientsQuery` | 200 | Security / DoctorOnboarding |

| E04 | GET `/api/v1/patients/me` | PatientProfile.ViewOwn + active linked Patient | `GetMyPatientProfileQuery` | 200 | Transition |

| E05 | PUT `/api/v1/patients/me` | PatientProfile.UpdateOwn + active linked Patient | `UpdateMyPatientProfileCommand` | 200 | Transition |

| E06 | GET `/api/v1/auth/me` | authenticated active account + existing live access checks | `MeQuery` | 200 | TrustAccess |

| N01 | POST `/api/v1/patients/full` | Patients.RegisterFull + active Reception assignment | `PatientIdentityCommand(Full)` | 201 | Registration |

| N02 | POST `/api/v1/patients/registration-matches` | Patients.RegistrationMatch + active Reception assignment | `PatientIdentityQuery(Matches) / PatientIdentityCommand(ConfirmReuse|ConfirmClaimIdentity)` | 200 | Security |

| N03 | POST `/api/v1/patient-onboarding/start` | anonymous Register/Claim; active Patient EmailChange | `PatientIdentityCommand(Start)` | 202 | Registration / Security / Transition |

| N04 | POST `/api/v1/patient-onboarding/verify-email` | opaque challenge and correct OTP; EmailChange bound to current Patient | `PatientIdentityCommand(Verify)` | 200 | Registration / Security / Transition |

| N05 | POST `/api/v1/patient-onboarding/register` | anonymous RegisterNew continuation for same email | `PatientIdentityCommand(Register)` | 201 | Registration |

| N06 | POST `/api/v1/patient-onboarding/claims` | anonymous Claim continuation + independent evidence | `PatientIdentityCommand(SubmitClaim)` | 201 | Claims / Transition |

| N07 | GET `/api/v1/patient-onboarding/claims/{id}` | X-Onboarding-Token, same verified applicant | `PatientIdentityQuery(OwnClaim)` | 200 | Claims (owned status and denial) |

| N08 | POST `/api/v1/patient-onboarding/claims/{id}/resubmit` | fresh unconsumed X-Onboarding-Token, same applicant | `PatientIdentityCommand(ResubmitClaim)` | 200 | Transition |

| N09 | POST `/api/v1/patient-onboarding/activate` | anonymous one-use invitation | `PatientIdentityCommand(Activate)` | 200 | Registration / Claims / native Transition |

| N10 | POST `/api/v1/patient-onboarding/activation/resend` | generic Full; Claim requires verified same-email Claim proof | `PatientIdentityCommand(Resend)` | 202 | Claims expiry/resend |

| N11 | GET `/api/v1/admin/patient-claims` | SuperAdmin + PatientClaims.ViewAll (live DB) | `PatientIdentityQuery(Claims)` | 200 | Claims list and Security denial |

| N12 | GET `/api/v1/admin/patient-claims/{id}` | SuperAdmin + PatientClaims.ViewDetails (live DB) | `PatientIdentityQuery(ClaimDetails)` | 200 | Claims / Transition |

| N13 | GET `/api/v1/admin/patient-claims/{id}/evidence/{evidenceId}` | SuperAdmin + PatientClaims.ViewEvidence (live DB/resource) | `PatientIdentityQuery(ClaimEvidence)` | 200 | Claims |

| N14 | POST `/api/v1/admin/patient-claims/{id}/approve` | SuperAdmin + PatientClaims.Approve (live DB) | `PatientIdentityCommand(Approve)` | 200 | Claims / native Transition |

| N15 | POST `/api/v1/admin/patient-claims/{id}/reject` | SuperAdmin + PatientClaims.Reject (live DB) | `PatientIdentityCommand(Reject)` | 200 | Transition |

| N16 | POST `/api/v1/admin/patient-claims/{id}/request-modification` | SuperAdmin + PatientClaims.RequestModification (live DB) | `PatientIdentityCommand(Modify)` | 200 | Transition |

| N17 | GET `/api/v1/admin/patient-claims/{id}/history` | SuperAdmin + PatientClaims.ViewDetails (live DB) | `PatientIdentityQuery(ClaimHistory)` | 200 | Claims immutable history route |

| N18 | GET `/api/v1/admin/patient-duplicates` | SuperAdmin + PatientDuplicates.View (live DB) | `PatientIdentityQuery(Duplicates)` | 200 | Duplicate API / domain |

| N19 | GET `/api/v1/admin/patient-duplicates/{id}` | SuperAdmin + PatientDuplicates.View (live DB) | `PatientIdentityQuery(DuplicateDetails)` | 200 | Duplicate API / domain |

| N20 | POST `/api/v1/admin/patient-duplicates/{id}/resolve` | SuperAdmin + PatientDuplicates.Resolve (live DB) | `PatientIdentityCommand(ResolveDuplicate)` | 200 | Duplicate API / domain |

## Shared contracts

JSON uses camelCase and string enum names. DateOnly is YYYY-MM-DD; timestamps UTC ISO-8601 Z; rowVersion is opaque Base64. `Idempotency-Key` is a UUID <=100 characters for new writes, E01 and N05. N03/N04 use challenge controls; N09 uses its invitation. N02 Search needs no key. Existing E02/E05/booking header compatibility is retained. Reuse the same key and identical body for a network retry; a changed body conflicts. File content hashes participate in fingerprints.

All examples below use shell environment placeholders, synthetic .test addresses and fake contact numbers. Populate secrets interactively; do not commit them. For authenticated routes send `Authorization: Bearer $ACCESS_TOKEN`. For N07/N08 use `X-Onboarding-Token: $PROOF`. Curl examples describe requests, not fabricated execution evidence.

No password, OTP, invitation token, clinical detail or private storage key is returned except N04 intentionally returning the opaque continuation token after email proof. Strong passwords use the existing service. All new writes are rate-limited; current DB roles/permissions and practice assignments are rechecked.

Registration/profile fields: nameAr 1..200; nameEn optional<=200; DOB nonfuture; gender Male/Female; phone<=30; identity/contact emails<=200 valid. Reception minimum phone rules and contact relationship/link semantics persist. Identity email trim/lower; no provider-specific rewriting. ContactEmail never establishes login/claim/clinical entitlement.

## Endpoint request and response reference

### E01 — POST `/api/v1/auth/patients/register`

Authorization: anonymous verified email. Input (multipart): emailVerificationToken,userName,email,phoneNumber,password,confirmPassword,nameAr,dateOfBirth,gender; optional nameEn,profileImage,personalIdFrontImage,personalIdBackImage

Success **201**: `VerifiedPatientRegistrationResponse`.

```bash
curl -X POST "$BASE/api/v1/auth/patients/register" -H "Idempotency-Key: $IDEMPOTENCY_KEY" -F "emailVerificationToken=$PROOF" -F "userName=synthetic-adult" -F "email=adult@example.test" -F "phoneNumber=01000000000" -F "password=$PATIENT_PASSWORD" -F "confirmPassword=$PATIENT_PASSWORD" -F "nameAr=مريض تجريبي" -F "dateOfBirth=1990-02-03" -F "gender=Male"
```

### E02 — POST `/api/v1/patients`

Authorization: Patients.Register + active Reception assignment. Input (multipart): nameAr,dateOfBirth,gender,doctorPracticeId; optional nameEn,phoneNumber,email,profileImage,primaryContactNameAr,primaryContactNameEn,primaryContactPhoneNumber,primaryContactRelationshipType,primaryContactLinkedPatientId,primaryContactIsPrimary,primaryContactEmail. If soft matches: matchReviewOutcome=DistinctPerson,matchReviewReason,matchingAttestationBasis. Require patient phone OR usable primary contact.

Success **201**: `{patientId,profileType:"Basic",accountLinked:false}`.

```bash
curl -X POST "$BASE/api/v1/patients" -H "Authorization: Bearer $ACCESS_TOKEN" -H "Idempotency-Key: $IDEMPOTENCY_KEY" -F "doctorPracticeId=$PRACTICE" -F "nameAr=مريض تجريبي" -F "dateOfBirth=1990-02-03" -F "gender=Male" -F "phoneNumber=01000000000" -F "email=adult@example.test"
```

### E03 — GET `/api/v1/patients/search`

Authorization: Patients.SearchBasic + active Reception assignment. Input (query): doctorPracticeId; email exact OR phoneNumber>=7 digits OR name>=3+dateOfBirth; pageNumber=1,pageSize=20 max100. DOB alone forbidden.

Success **200**: `{items:[{patientId,nameAr,nameEn,dateOfBirth,gender,phoneNumberMasked,emailMasked,hasContactPhone}],totalCount,pageNumber,pageSize}`.

```bash
curl -X GET "$BASE/api/v1/patients/search?doctorPracticeId=$PRACTICE&email=adult%40example.test" -H "Authorization: Bearer $ACCESS_TOKEN"
```

### E04 — GET `/api/v1/patients/me`

Authorization: PatientProfile.ViewOwn + active linked Patient. Input (none): No request body. IDs are canonical GUIDs.

Success **200**: `PatientProfileResponse + accountStatus,hasVerifiedIdentityEmail,identityEmailMasked`.

```bash
curl -X GET "$BASE/api/v1/patients/me" -H "Authorization: Bearer $ACCESS_TOKEN"
```

### E05 — PUT `/api/v1/patients/me`

Authorization: PatientProfile.UpdateOwn + active linked Patient. Input (multipart): nameAr,nameEn?,phoneNumber?,email?,profileImage?,rowVersion; emailVerificationToken required if normalized email changes; cannot clear a linked identity email.

Success **200**: `PatientProfileResponse`.

```bash
curl -X PUT "$BASE/api/v1/patients/me" -H "Authorization: Bearer $ACCESS_TOKEN" -F "nameAr=مريض تجريبي" -F "phoneNumber=01000000000" -F "email=changed@example.test" -F "rowVersion=$ROW_VERSION" -F "emailVerificationToken=$PROOF"
```

### E06 — GET `/api/v1/auth/me`

Authorization: authenticated active account + existing live access checks. Input (none): No request body. IDs are canonical GUIDs.

Success **200**: `existing current-user contract + accountStatus,patientIdentityEmailVerified`.

```bash
curl -X GET "$BASE/api/v1/auth/me" -H "Authorization: Bearer $ACCESS_TOKEN"
```

### N01 — POST `/api/v1/patients/full`

Authorization: Patients.RegisterFull + active Reception assignment. Input (multipart): Basic identity/contact fields plus REQUIRED email,verifiedInPerson=true,identityVerificationBasis=GovernmentIdSeen|ApprovedAlternateEvidence; optional verificationNote<=500. Soft matches require matchReviewOutcome=DistinctPerson,matchReviewReason<=500. No password fields.

Success **201**: `{patientId,applicationUserId,profileType:"Full",accountStatus:"PendingActivation",activationEmailMasked,invitationExpiresOnUtc}`.

```bash
curl -X POST "$BASE/api/v1/patients/full" -H "Authorization: Bearer $ACCESS_TOKEN" -H "Idempotency-Key: $IDEMPOTENCY_KEY" -F "doctorPracticeId=$PRACTICE" -F "nameAr=مريض تجريبي" -F "dateOfBirth=1990-02-03" -F "gender=Male" -F "phoneNumber=01000000000" -F "email=adult@example.test" -F "verifiedInPerson=true" -F "identityVerificationBasis=GovernmentIdSeen"
```

### N02 — POST `/api/v1/patients/registration-matches`

Authorization: Patients.RegistrationMatch + active Reception assignment. Input (json): Search: mode,doctorPracticeId,email?,phoneNumber?,nameAr?,dateOfBirth?,gender?,pageSize=20 (max20). ConfirmReuse: mode,doctorPracticeId,patientId,verifiedInPerson=true,verificationBasis,reason?<=500. ConfirmClaimIdentity: mode,doctorPracticeId,patientId,claimantEmail,verifiedInPerson=true,verificationBasis=GovernmentIdSeen|ApprovedAlternateEvidence,reason(required).

Success **200**: `Search result or PatientIdentityAttestationResponse`.

```bash
curl -X POST "$BASE/api/v1/patients/registration-matches" -H "Authorization: Bearer $ACCESS_TOKEN" -H "Idempotency-Key: $IDEMPOTENCY_KEY" -H "Content-Type: application/json" -d '{"mode":"Search","doctorPracticeId":"11111111-1111-1111-1111-111111111111","email":"adult@example.test"}'
```

### N03 — POST `/api/v1/patient-onboarding/start`

Authorization: anonymous Register/Claim; active Patient EmailChange. Input (json): email,purpose=Register|Claim|EmailChange,patientReference? (Claim only). EmailChange requires Patient bearer token.

Success **202**: `{requestId,message}`.

```bash
curl -X POST "$BASE/api/v1/patient-onboarding/start" -H "Content-Type: application/json" -d '{"email":"adult@example.test","purpose":"Register"}'
```

### N04 — POST `/api/v1/patient-onboarding/verify-email`

Authorization: opaque challenge and correct OTP; EmailChange bound to current Patient. Input (json): requestId,otp (6 digits default).

Success **200**: `{verificationToken,expiresOnUtc,nextAction}`.

```bash
curl -X POST "$BASE/api/v1/patient-onboarding/verify-email" -H "Content-Type: application/json" -d '{"requestId":"11111111-1111-1111-1111-111111111111","otp":"$OTP_FROM_EMAIL"}'
```

### N05 — POST `/api/v1/patient-onboarding/register`

Authorization: anonymous RegisterNew continuation for same email. Input (multipart): emailVerificationToken,userName,email,phoneNumber,password,confirmPassword,nameAr,dateOfBirth,gender; optional nameEn,profileImage,personalIdFrontImage,personalIdBackImage

Success **201**: `{patientId,applicationUserId,accountStatus:"Active"}`.

```bash
curl -X POST "$BASE/api/v1/patient-onboarding/register" -H "Idempotency-Key: $IDEMPOTENCY_KEY" -F "emailVerificationToken=$PROOF" -F "userName=synthetic-adult" -F "email=adult@example.test" -F "phoneNumber=01000000000" -F "password=$PATIENT_PASSWORD" -F "confirmPassword=$PATIENT_PASSWORD" -F "nameAr=مريض تجريبي" -F "dateOfBirth=1990-02-03" -F "gender=Male"
```

### N06 — POST `/api/v1/patient-onboarding/claims`

Authorization: anonymous Claim continuation + independent evidence. Input (multipart): emailVerificationToken,nameAr,dateOfBirth,gender,evidenceType (repeat for each); optional patientReference,phoneNumber,identityFront,identityBack,birthCertificate,guardianRelationshipDocument,inPersonIdentityAttestationId,verificationNote<=500. Evidence types: GovernmentIdFront,GovernmentIdBack,BirthCertificate,InPersonIdentityAttestation,GuardianRelationshipEvidence. File fields must match declared types. Reference/notes alone are insufficient.

Success **201**: `{claimRequestId,status:"PendingReview",submittedOnUtc,canAccessMedicalRecord:false}`.

```bash
curl -X POST "$BASE/api/v1/patient-onboarding/claims" -H "Idempotency-Key: $IDEMPOTENCY_KEY" -F "emailVerificationToken=$PROOF" -F "nameAr=مريض تجريبي" -F "dateOfBirth=1990-02-03" -F "gender=Male" -F "evidenceType=GovernmentIdFront" -F "identityFront=@synthetic.png;type=image/png"
```

### N07 — GET `/api/v1/patient-onboarding/claims/$CLAIM`

Authorization: X-Onboarding-Token, same verified applicant. Input (none): No request body. IDs are canonical GUIDs.

Success **200**: `{claimRequestId,status,submittedOnUtc,lastActivityOnUtc,reasonForApplicant?,canResubmit,canActivate,activationInvitationSent,closedOnUtc?,rowVersion}`.

```bash
curl -X GET "$BASE/api/v1/patient-onboarding/claims/$CLAIM" -H "X-Onboarding-Token: $PROOF"
```

### N08 — POST `/api/v1/patient-onboarding/claims/$CLAIM/resubmit`

Authorization: fresh unconsumed X-Onboarding-Token, same applicant. Input (multipart): rowVersion,evidenceType and new evidence files/attestation as N06; optional reasonResponse<=1000 and identityProfileCorrections.NameAr/DateOfBirth/Gender/PhoneNumber (ASP.NET dotted multipart names). X-Onboarding-Token required; email cannot change.

Success **200**: `{claimRequestId,status:"PendingReview",revisionNumber,rowVersion}`.

```bash
curl -X POST "$BASE/api/v1/patient-onboarding/claims/$CLAIM/resubmit" -H "X-Onboarding-Token: $PROOF" -H "Idempotency-Key: $IDEMPOTENCY_KEY" -F "rowVersion=$ROW_VERSION" -F "reasonResponse=New synthetic evidence" -F "evidenceType=GovernmentIdFront" -F "identityFront=@synthetic.png;type=image/png"
```

### N09 — POST `/api/v1/patient-onboarding/activate`

Authorization: anonymous one-use invitation. Input (json): invitationToken,password,confirmPassword; unknown JSON members rejected. No PatientId/email/UserType supplied.

Success **200**: `{patientId,applicationUserId,accountStatus:"Active",requiresLogin:true}`.

```bash
curl -X POST "$BASE/api/v1/patient-onboarding/activate" -H "Content-Type: application/json" -d '{"invitationToken":"$INVITATION_FROM_EMAIL","password":"$PATIENT_PASSWORD","confirmPassword":"$PATIENT_PASSWORD"}'
```

### N10 — POST `/api/v1/patient-onboarding/activation/resend`

Authorization: generic Full; Claim requires verified same-email Claim proof. Input (json): email,claimRequestId?,verificationToken? (required for Claim).

Success **202**: `{message}`.

```bash
curl -X POST "$BASE/api/v1/patient-onboarding/activation/resend" -H "Idempotency-Key: $IDEMPOTENCY_KEY" -H "Content-Type: application/json" -d '{"email":"adult@example.test"}'
```

### N11 — GET `/api/v1/admin/patient-claims`

Authorization: SuperAdmin + PatientClaims.ViewAll (live DB). Input (query): status?,fromUtc?,toUtc?,emailSearch? exact normalized; pageNumber=1,pageSize=20 max100.

Success **200**: `{items:[{id,status,claimantEmailMasked,submittedOnUtc,lastActivityOnUtc,evidenceCount,rowVersion}],totalCount,pageNumber,pageSize}`.

```bash
curl -X GET "$BASE/api/v1/admin/patient-claims?status=PendingReview&pageNumber=1&pageSize=20" -H "Authorization: Bearer $ACCESS_TOKEN"
```

### N12 — GET `/api/v1/admin/patient-claims/$CLAIM`

Authorization: SuperAdmin + PatientClaims.ViewDetails (live DB). Input (none): No request body. IDs are canonical GUIDs.

Success **200**: `{claimRequestId,status,claimant:{nameAr,dateOfBirth,gender,email,phoneMasked},patientReference,candidatePatients:[{patientId,nameAr,dateOfBirth,gender,emailMasked}],evidence:[{evidenceId,type,revisionNumber,uploadedOnUtc}],historySummary,submittedOnUtc,lastActivityOnUtc,rowVersion,reviewCapabilities:{canApprove,canReject,canRequestModification}}`.

```bash
curl -X GET "$BASE/api/v1/admin/patient-claims/$CLAIM" -H "Authorization: Bearer $ACCESS_TOKEN"
```

### N13 — GET `/api/v1/admin/patient-claims/$CLAIM/evidence/$EVIDENCE`

Authorization: SuperAdmin + PatientClaims.ViewEvidence (live DB/resource). Input (none): No request body. IDs are canonical GUIDs.

Success **200**: `private file stream, Cache-Control:no-store; access audited`.

```bash
curl -X GET "$BASE/api/v1/admin/patient-claims/$CLAIM/evidence/$EVIDENCE" -H "Authorization: Bearer $ACCESS_TOKEN"
```

### N14 — POST `/api/v1/admin/patient-claims/$CLAIM/approve`

Authorization: SuperAdmin + PatientClaims.Approve (live DB). Input (json): targetPatientId,rowVersion,reviewedEvidenceIds (current revision),reviewNote?<=1000; reason required if selecting different applicant reference.

Success **200**: `{claimRequestId,status:"ApprovedPendingActivation",targetPatientId,rowVersion,invitationExpiresOnUtc}`.

```bash
curl -X POST "$BASE/api/v1/admin/patient-claims/$CLAIM/approve" -H "Authorization: Bearer $ACCESS_TOKEN" -H "Idempotency-Key: $IDEMPOTENCY_KEY" -H "Content-Type: application/json" -d '{"targetPatientId":"11111111-1111-1111-1111-111111111111","rowVersion":"$ROW_VERSION","reviewedEvidenceIds":["22222222-2222-2222-2222-222222222222"],"reviewNote":"Synthetic proof reviewed"}'
```

### N15 — POST `/api/v1/admin/patient-claims/$CLAIM/reject`

Authorization: SuperAdmin + PatientClaims.Reject (live DB). Input (json): rowVersion,reason 1..1000. Only PendingReview.

Success **200**: `{claimRequestId,status:"Rejected",closedOnUtc,rowVersion,canResubmit:false}`.

```bash
curl -X POST "$BASE/api/v1/admin/patient-claims/$CLAIM/reject" -H "Authorization: Bearer $ACCESS_TOKEN" -H "Idempotency-Key: $IDEMPOTENCY_KEY" -H "Content-Type: application/json" -d '{"rowVersion":"$ROW_VERSION","reason":"Additional independent evidence required"}'
```

### N16 — POST `/api/v1/admin/patient-claims/$CLAIM/request-modification`

Authorization: SuperAdmin + PatientClaims.RequestModification (live DB). Input (json): rowVersion,reason 1..1000. Only PendingReview.

Success **200**: `{claimRequestId,status:"ModificationRequested",rowVersion,canResubmit:true,closedOnUtc:null}`.

```bash
curl -X POST "$BASE/api/v1/admin/patient-claims/$CLAIM/request-modification" -H "Authorization: Bearer $ACCESS_TOKEN" -H "Idempotency-Key: $IDEMPOTENCY_KEY" -H "Content-Type: application/json" -d '{"rowVersion":"$ROW_VERSION","reason":"Additional independent evidence required"}'
```

### N17 — GET `/api/v1/admin/patient-claims/$CLAIM/history`

Authorization: SuperAdmin + PatientClaims.ViewDetails (live DB). Input (none): No request body. IDs are canonical GUIDs.

Success **200**: `{items:[{eventId,revisionNumber,action,fromStatus,toStatus,actorId,occurredOnUtc,reason}]}`.

```bash
curl -X GET "$BASE/api/v1/admin/patient-claims/$CLAIM/history" -H "Authorization: Bearer $ACCESS_TOKEN"
```

### N18 — GET `/api/v1/admin/patient-duplicates`

Authorization: SuperAdmin + PatientDuplicates.View (live DB). Input (query): status?,pageNumber=1,pageSize=20 max100.

Success **200**: `{items:[{caseId,firstPatientId,secondPatientId,status,createdOnUtc,rowVersion}],totalCount,pageNumber,pageSize}`.

```bash
curl -X GET "$BASE/api/v1/admin/patient-duplicates?status=NeedsMoreEvidence&pageNumber=1&pageSize=20" -H "Authorization: Bearer $ACCESS_TOKEN"
```

### N19 — GET `/api/v1/admin/patient-duplicates/$CLAIM`

Authorization: SuperAdmin + PatientDuplicates.View (live DB). Input (none): No request body. IDs are canonical GUIDs.

Success **200**: `{caseId,status,patientA:{id,nameAr,dateOfBirth,gender,emailMasked},patientB:{id,nameAr,dateOfBirth,gender,emailMasked},matchIndicators,createdOnUtc,rowVersion}`.

```bash
curl -X GET "$BASE/api/v1/admin/patient-duplicates/$CLAIM" -H "Authorization: Bearer $ACCESS_TOKEN"
```

### N20 — POST `/api/v1/admin/patient-duplicates/$CLAIM/resolve`

Authorization: SuperAdmin + PatientDuplicates.Resolve (live DB). Input (json): resolution=ConfirmedDuplicate|DistinctPatients|NeedsMoreEvidence,reason 1..1000,rowVersion. Final cases cannot reopen.

Success **200**: `{caseId,status,rowVersion}`.

```bash
curl -X POST "$BASE/api/v1/admin/patient-duplicates/$CLAIM/resolve" -H "Authorization: Bearer $ACCESS_TOKEN" -H "Idempotency-Key: $IDEMPOTENCY_KEY" -H "Content-Type: application/json" -d '{"resolution":"DistinctPatients","reason":"Independent identities verified","rowVersion":"$ROW_VERSION"}'
```

## Matching, continuation and state outcomes

N02 Search returns `{matchOutcome,candidates,canCreateBasic,canCreateFull,requiresClaimForAccount}`; candidates are `{patientId,nameAr,dateOfBirth,gender,emailMasked,phoneMasked,isLinkedAccount}`. Outcomes: NoMatch/PossibleMatch/MultipleMatches/AlreadyLinked/IdentityConflict. Exact email never permits duplicate creation; phone/demographics never prove identity. Soft registration overrides require explicit attested DistinctPerson reason and open classification cases. ConfirmReuse/ConfirmClaimIdentity return `{patientId,attestationId,expiresOnUtc}` with 15-minute / 48-hour lifetimes respectively.

N04 nextAction: RegisterNew / ClaimExisting / LoginOrRecovery / IdentityConflict / EmailChange. A verified Register challenge for Basic becomes ClaimExisting in the same response. Per owner clarification, a verified Claim without exact email/reference receives Claim-scoped proof; N06 evaluates plausible unlinked demographic candidates and evidence, otherwise NoClaimableProfile. Tokens cannot change action by client input. They allow one mutation; read-only N07 remains usable until 30-minute expiry even after consumption.

Claim: PendingReview -> ModificationRequested -> PendingReview (new immutable evidence revision), PendingReview -> Rejected, PendingReview -> ApprovedPendingActivation -> Linked; eligible nonterminal inactivity -> Expired. Cancelled is internal only. No cancel route. Approval creates invitation only. Claim activation links the original PatientId and retains medical/Family/appointment relationships. No clinical permission before activation.

Full: PendingActivation user is inactive with an unguessable unusable hash and existing Full source link. Patient chooses the real password at N09. Resend revokes prior invitation without recreating Patient/User. N09 issues no JWT; normal Login follows.

## Existing scheduling reuse — route correction approved by owner

Retain `POST /api/v1/reception/practices/{practiceId}/reservations` and **current** `POST /api/v1/practices/{practiceId}/tickets/walk-in`. The task's `/reception/.../tickets/walk-in` reference was incorrect; owner explicitly instructed keeping the current route. These are existing APIs, outside N01–N20. Add optional `patientIdentityAttestationId` to existing DTOs. Same-practice established Reservation/Ticket relationships preserve prior behavior. For a new foreign-practice relationship obtain N02 ConfirmReuse and send actual PatientId plus attestation. Attestation must be same actor/practice/patient, unexpired and unused; it is associated in the booking transaction. No clinical access is inferred.

## Error and old-client migration

The existing BuildingBlock ProblemDetails envelope remains `{type,title,status,detail,instance,...,errors:[{code,message,...}]}`. Product result filtering maps malformed Phase 5 validation to400 while keeping age/proof rejections422. Unrelated endpoint mappings remain intact. Arabic resources and English translations are used; error text contains no email, phone, OTP, token or document content.

| HTTP | Stable codes / client action |

|---|---|

| 400 | Patient.Invalid; PatientOnboarding.OtpInvalidOrExpired; malformed fields/header |

| 401/403/404 | authentication / permission denied / unavailable own claim or file; do not infer identity from404 |

| 409 | Patient.AlreadyExists (Reception search/reuse), Patient.ClaimRequired (Claim), Patient.AlreadyLinked, Patient.IdentityConflict, Patient.NoClaimableProfile, Identity.EmailOwnedByAnotherAccount, Identity.AlreadyRegistered, Patient.IdentityAttestationRequired/Invalid, Patient.EmailVerificationRequired, PatientOnboarding.VerificationRequired, PatientOnboarding.TokenInvalidOrExpired, PatientActivation.InvalidOrExpired, PatientClaims.InvalidState/PendingAlreadyExists/ConcurrencyConflict, PatientDuplicates.InvalidResolution, PatientIdentity.IdempotencyKeyReused |

| 422 | Patient.FullRegistrationAgeRestricted; Patient.SelfRegistrationAgeRestricted; PatientClaims.ProofInsufficient |

| 429 | PatientOnboarding.TooManyAttempts; PatientIdentity.RateLimited; obey Retry-After |

Legacy E01 is intentionally breaking: obtain email proof via N03/N04, add emailVerificationToken and Idempotency-Key. Missing proof returns actionable VerificationRequired with no write. Both E01 and N05 call the same workflow. Under18 direct self/full fails422; age16–17 uses existing Basic + manual Claim, under16 no standalone login. E03 replaces raw phoneNumber with phoneNumberMasked and adds emailMasked; send bounded search criteria. E05 retains email field but verified changes require exact-email EmailChange proof. All other existing profile properties are preserved.

Coverage labels refer to `Phase5Final*Tests.cs` and named existing regression suites. Pending coverage is explicitly tracked and is not a green acceptance claim.

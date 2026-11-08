# Family self-service API handoff (in progress)

See [API-HANDOFF-EXTENSIONS.md](API-HANDOFF-EXTENSIONS.md) for added profile, historical, diagnostic and finance routes.

The owner epic remains the full acceptance scope. This file describes the routes
present in this branch, not a claim that every epic feature has been completed.
All routes use `/api/v1`, authenticated verified active Patient accounts for
self-service, current JWT AND database permissions, and no-store responses for
new family views. Enumerations retain existing string JSON serialization.

| Method | Route | Permission / actor | Contract |
|---|---|---|---|
| GET | `/families/mine/workspace` | `FamilyWorkspace.ViewOwn`, adult Patient | 200 even without Family; separate per-child operational/medical capabilities; unfinished actions false |
| POST | `/families/patient-candidates/search` | `PatientFamilyCandidates.Search`, adult Patient | JSON NameAr AND DOB AND Gender, optional phone; bounded masked opaque references; ambiguous identity requires review |
| POST | `/family-relationship-requests/from-candidate` | `PatientFamilyCandidates.Submit`, adult Patient | multipart candidateReference, Father/Mother relationshipType, evidenceFiles/documentTypes, Idempotency-Key; 202 pending; no PatientId/FamilyId |
| POST | `/families/mine/child-onboarding` | `ChildOnboarding.CreateOwn`, adult Patient | multipart identity/contact/parent-role/evidence, Idempotency-Key; 202; no definitive Patient before review |
| GET | `/families/mine/child-onboarding-requests[/{requestId}]` | `ChildOnboarding.ViewOwn` | own private intake, evidence metadata and status; bounded list, no existing-match identifiers |
| POST | `/families/mine/child-onboarding-requests/{requestId}/resubmit` | `ChildOnboarding.ResubmitOwn` | evidence-only new revision after modification request; RowVersion and Idempotency-Key |
| GET | `/admin/child-onboarding-requests[/{requestId}]` | `ChildOnboarding.ViewAll` / `.ViewDetails`, SuperAdmin | private intake review; no clinical document entitlement |
| GET | `/admin/child-onboarding-requests/{requestId}/evidence/{documentId}/content` | `ChildOnboarding.ViewEvidence`, SuperAdmin | private stream, fresh authorization, no-store/nosniff |
| POST | `/admin/child-onboarding-requests/{requestId}/approve` | `ChildOnboarding.Decide`, SuperAdmin | RowVersion, Idempotency-Key, optional confirmedExistingPatientId selected after independent identity review; also requires existing family approval permission |
| POST | `/admin/child-onboarding-requests/{requestId}/reject` | `ChildOnboarding.Decide`, SuperAdmin | reason, RowVersion, Idempotency-Key |
| POST | `/admin/child-onboarding-requests/{requestId}/request-modification` | `ChildOnboarding.Decide`, SuperAdmin | reason, RowVersion, Idempotency-Key |
| POST | `/admin/family-operational-authorities/{authorityId}/revoke` | `FamilyOperationalProofRequests.Revoke`, SuperAdmin | reason, RowVersion, Idempotency-Key; append-only history; no record deletion |
| GET | `/admin/family-operational-authorities?relationshipRequestId=...` | `FamilyOperationalProofRequests.Revoke`, SuperAdmin | bounded exact-request provenance and RowVersion for governance/revocation |
| GET | `/families/mine/dependents/{childPatientId}/profile` | `DependentPatientProfile.View` | exact current operational pair, under18; excludes identity email and identity document keys |
| GET | `/tickets/dependents/{childPatientId}/active` | `DependentTickets.ViewAuthorized` | exact operational pair, under18; safe active queue descriptors |
| GET | `/tickets/dependents/{childPatientId}/{ticketId}` | `DependentTickets.ViewAuthorized` | subject equality; safe status/date/queue fields; no clinical IDs, payment details or queue controls |

Existing reservations, bookable-patients, reservation history and follow-up retain
their routes and DTOs but now require exact operational proof for dependent
subjects. Existing `/tickets/mine`, `/patients/me`, medical record and finance
ownership scopes remain unchanged. Archived dependent ticket detail is not
enabled by the new ticket routes.

Security breaking changes to legacy family requests:

- Raw TargetPatientId can no longer bootstrap self-service family membership.
  Use candidate search or child intake. Same-family verification retains its
  existing reviewed flow.
- Own request lists redact TargetPatientId to null and TargetNameAr to `***`.
  Non-own requester identities in related legacy rows are masked.
- Non-admin details return `target: null`, `familyMembers: []`, and no FamilyId.
  History does not expose reviewer user IDs. Admin review retains its separate
  permissioned identity/evidence view.
- Reviewer free text remains private. Applicant responses use localized safe
  feedback, history omits private review messages, and status emails do not
  distribute confidential reviewer text. Admin detail retains it.
- Self-service resubmission does not disclose the confidential target FamilyId.

Existing enum values 1–4 are unchanged. Appended request types are
`VerifyOperationalParentChild=5` and `JoinFamilyAsParent=6`. The former explicitly
does not create clinical proof. A second parent can submit an independently
reviewed selected-child case to join the existing Family. Membership conflicts
return a non-disclosing conflict rather than transferring anyone automatically.

Frontend flow: open workspace directly; choose Find Existing Child or Register
New Child; retain only opaque candidateReference for selection; upload evidence;
track own requests; wait for review. Never render null/empty redacted identifiers
as navigation targets. Use independent capability flags. Contact/photo writes,
identity corrections, historical documents, dependent finance and proxy diagnostic
UI must remain unavailable until their implementations and checks are complete.
No historical Timeline subtype is supplied by this slice; official counts and
existing timeline discriminators remain unchanged.

New errors use English/Arabic resources. Normal authentication/permission errors
are 401/403, unrelated subject/reference reads are opaque 404, stale/replayed or
duplicate intent is 409, validation follows the existing ProblemDetails mapping,
and search throttling returns 429. Cross-Family conflicts require confidential
governance; no automatic transfer is approved.

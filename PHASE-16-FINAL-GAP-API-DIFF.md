# Phase 16 final gap closure — exact API diff and frontend handoff

2026-10-24. Baseline main `898526bd9de4bf28bea9fa47d26b9ff7374d49e0`.
**Zero new HTTP routes; zero removed/renamed routes; zero new permissions.**
All paths below use `/api/v1`. The complete unchanged P16-01–29 signatures remain
in [the route inventory](PHASE-16-API-CHANGES.md).

| Existing surface | Contract/behavior change |
|---|---|
| P16-01–06 Patient | None; safe own completed Visible/Hidden outputs and four-field access log retained |
| P16-07–11 treating Doctor | None; exact Ticket/Encounter InProgress, practice, patient and live permission checks retained |
| P16-12–21 Hidden requests | None; source Doctor only, exact treatment/revision grant, no patient/emergency stage |
| P16-22–23 visibility | None; source ownership, history and atomic completion retained |
| P16-24 `GET /families/mine/medical-record-dependents` | Same response shape; omit unreadable children. Verified parents need no eligibility row; Guardians need exact proof plus current approval |
| P16-25–29 `GET /families/mine/dependents/{dependentPatientId}/medical-record/{overview,timeline,encounters/{encounterId},items/{resourceType}/{sourceId},items/{resourceType}/{sourceId}/attachments/{attachmentId}/content}` | Same signatures and patient-safe DTOs; centrally apply the new parent/Guardian branch, under-16/restriction/current permission/audit gates |
| `GET /families/mine` | Existing child medicalAccess projection reflects new entitlement |
| `GET /families/mine/dependents/{childPatientId}/medical-access` | Same seven fields; `RelationshipNotEligible` may use safe reason `ParentChildVerificationRequired` |
| `POST /family-relationship-requests`, `POST /family-relationship-requests/assisted` | Same multipart fields; requestType appends VerifyParentChild=3 / VerifyGuardianChild=4. CreateFamily=1/AddFamilyMember=2 unchanged |
| Existing relationship GET/list/document, POST resubmit and admin approve/reject/request-modification routes | Existing contracts retained; new request types follow the same evidence/revision/review journey. Verification never adds a member |
| `GET /admin/family-medical-eligibilities/reviews` | New candidates/list targets Guardian/LegalGuardian. Historical parent detail/history retained through existing detail route |
| `GET /admin/family-medical-eligibilities/{reviewId}` | Adds nullable authorityRelationshipRequestId; canApprove supports authorized explicit re-verification, including Approved/Rejected/Revoked Guardian cases |
| `POST /admin/family-medical-eligibilities/{reviewId}/approve` | Existing decision fields retained; additive authorityRelationshipRequestId required for approval and bound to exact current approved authority/evidence. Existing RowVersion remains required |
| Eligibility reject/revoke/history | Routes/contracts retained; historical parent decisions do not change parent entitlement. New administrative history messages retain approval policy/basis/authority provenance or rejection/revocation reason |
| `POST /admin/family-medical-access-requests/{requestId}/approve` | Existing basisReference is used for the approved restriction ground. New restrictions require that reference in signed RestrictionGroundsReferences and current real private evidence. Restoration retains existing behavior |
| Other restriction/restoration routes | All retained; Pending has no blocking effect, effective legacy restrictions remain, immutable histories preserved. A new restriction approval adds a RestrictionGroundReviewed administrative history event; the existing approval event keeps the full decision note |

## Verification multipart journey

Submit RequestType=VerifyParentChild, existing FamilyId, TargetPatientId=child,
RequesterClaimedRole=Father/Mother, TargetClaimedRole=Child, actual EvidenceFiles
and matching DocumentTypes through the existing route. The ordinary requester
comes from the current verified account link. Assisted submission adds the
existing RequesterPatientId under Reception permissions. Both members must
already be active in that same active Family and roles must match exactly.
VerifyGuardianChild uses Guardian/LegalGuardian and the same Child target.

Pending, ModificationRequested and Rejected cases grant no entitlement. Approved
parent proof records exact member IDs, reviewer, request/revision and policy.
Approved Guardian proof alone grants no medical read; submit the existing
eligibility approval with its authorityRelationshipRequestId afterward. Another
child's request, an adult-adult membership proof, missing/invalid current evidence,
stale RowVersion or unavailable signed policy cannot authorize an approval.

The decision body's existing rowVersion, reason, message, decisionNote,
policyVersion and basisReference are preserved. All clients must add the authority
reference on Guardian approvals. Old clients omitting it receive validation
failure; they are not granted an implicit authority source. Clients approving new
restrictions must submit an accepted basisReference and actual evidence in the
case's current revision. No new legal ground or document type is supplied by code.

## Status, pagination and compatibility

FamilyMedicalAccessStatusResponse retains status, canView, canRequestRestriction,
canRequestRestoration, activeOwnRequestId, activeOwnRequestType and
unavailableReasonCode. Parents with exact proof canView even with no eligibility
or historical Pending/Rejected/Revoked eligibility. Effective restrictions deny
every adult role. Missing parent proof reports ParentChildVerificationRequired.
Guardian unavailability remains EligibilityReviewPending/EligibilityUnavailable.
Parents do not gain a separate per-pair medical review step solely because a
configured global policy version changes; current allowed evidence and all gates
still apply, and old cursors are invalidated. Guardian eligibility must match the
current medical policy version and basis.
Existing family case submission capabilities remain separate from clinical reads.

Protected cursors now incorporate parent proof/member/request/evidence state and
Guardian authority alongside restrictions, policy and local date. On stale cursor
422, refresh from page one. Direct unavailable records/downloads retain sanitized
404; permission middleware may return its existing 403. The eligible list omits
unreadable children. At local midnight on the 16th birthday all new reads stop;
Family membership and booking authority do not change.

Patient-safe Hidden projections, no-store/nosniff, private attachment rechecks,
fail-closed audit and localized error conventions are retained. No patient sharing
toggle, consent workflow, emergency route, clinical retention/disposal route or
SuperAdmin/Reception clinical bypass is added.

## Operator and rollout contract

The maintenance CLI adds no HTTP endpoint. Run
`dotnet run --project tools/Wasla.FamilyProofMaintenance -c Release -- report`
before a reviewed `apply` run. Supply WASLA_FAMILY_MAINTENANCE_CONNECTION,
WASLA_FAMILY_OPERATOR_IDENTIFIER, WASLA_FAMILY_OPERATOR_PASSWORD and signed
FamilyMedicalPolicy configuration through environment/secret storage.
Supply the same private MediaStorage root/configuration used by the API; an
incorrect or unavailable evidence root fails closed. Fresh
SuperAdmin credentials and existing relationship Approve/ViewDetails permissions
are required. Output contains aggregate reconciliation counts only. No startup
auto-backfill or family-wide inferred grant exists.

Deploy additive schema before application instances; retain schema/history on
rollback and disable sharing. Do not run the tool or migrations against Production
without independent authorization. Production/Staging flags remain false.
Keep sharing disabled throughout a mixed-version rollout: old nodes do not enforce
the new exact parent proof rule.

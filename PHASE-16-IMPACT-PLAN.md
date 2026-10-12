# Phase 16 code impact and baseline inventory

Baseline: clean main at `563e2111182f584986f225a3711a459a00e3b913`.
Development branch: `codex/phase16-medical-record`. No AGENTS.md found in the
workspace or its parent. No full master v1.2 file is checked in.

Ordered review packages:

1. A: Domain Clinical/MedicalEncounter; clinical EF mappings; permission catalog
   and repeatable role seed; safe migration; CompleteTicket and TicketsController;
   owner clinical details/list; visibility command/history API and tests.
2. B: Hidden access aggregate/history; constraints; scoped tokens; transactional
   request/decision CQRS; effective expiry and completion integration; global inbox.
3. C: central patient projections over existing clinical/prescription/diagnostic
   sources; cursor pagination; private media; metadata audits and patient access log.
4. D: live exact-ticket access policy; gated doctor gateway and strict hidden union.
5. E: separately gated family contract; guardian implementation cannot ship until
   exact legal/privacy policy is approved.
6. F: route/serialization/security/lifecycle/SQL concurrency checks and rollout docs.

Existing routes to preserve: practice tickets start/complete/detail/walk-in and
reservation check-in/queue; doctor practices encounters list/detail/by-ticket,
notes, diagnoses, amendments and follow-up; encounters/mine; prescriptions/mine
and doctor-owned prescription APIs; lab/radiology requests/results/submissions
and private media; families/mine and relationship verification; patient profiles.
Controllers: ClinicalControllers, TicketsController, MedicationControllers,
LabControllers, RadiologyControllers, FamiliesController, PatientsController.

Reuse: Result/Error and localized ProblemDetails; MediatR transaction behavior;
ITicketQueueLock SQL transaction applocks; optimistic RowVersion; immutable
prescription/diagnostic histories; current DB access snapshot; private media.
The API already supports string enum names and numeric compatibility.

Risks: owner DTOs contain notes and cannot be shared; existing patient diagnosis
DTO includes Notes; legacy migration touches SQL rowversions (clients must
refetch after deployment); approval/read races require current committed state;
timeline must filter source encounters before projecting clinical fields; audit
failure must prevent disclosure. Master governance approval remains outstanding.

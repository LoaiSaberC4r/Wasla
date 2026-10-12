# Phase 16 family implementation impact and verification plan

Inspected baseline: `6044ed0a08a6ca959d54ee4f787c7c768d90801d`, PR #21 merged.
The existing branch was advanced to that merge without changing its name or files.
The supplied task is the product authority for decisions 01–28. The complete
Master SoT v1.2 is absent from this checkout/context; its consent OPEN status and
the existing proposed addendum remain release constraints.

Before production code, the impact inventory is:

| Layer | Change | Compatibility |
|---|---|---|
| Domain | Separate case, restriction, eligibility review, evidence and immutable histories | No clinical aggregate or family membership rewrite |
| Application | Transactional CQRS, validators, one current family medical decision interface, policy configuration | Existing booking under 18 stays unchanged |
| Infrastructure | Private evidence, compensation, SQL pair locks, idempotency, rowversion, batched capabilities and indexed queues | Existing patient-safe longitudinal projections reused |
| SQL | Additive tables, restrictive FKs, open-case/active-restriction uniqueness, Pending-only eligibility candidates | No automatic entitlement or clinical backfill |
| API | Existing six dependent routes activated behind full gate; 21 new routes; additive child capabilities | All existing routes preserved |
| Security | Append stable own/admin permissions; active identity and current database grants | No administrative clinical bypass |
| Governance | Proposed family addendum, frontend contract, actual closure report and release checklist | Both sharing flags remain false |

Verification packages: domain transitions and age boundary; authenticated family
journeys A–G including all six clinical routes and private evidence/media; exact
OpenAPI inventory; policy/permission/account/member/eligibility/restriction
revocation; SQL two-host duplicate and competing-decision races; additive
migration/backfill and repeatable seeding; family capabilities/inbox pagination
performance; full Release Wasla suite and separately reported BuildingBlock debt.
Tests use explicitly configured development proof-policy fixtures, never legal
approval. Keep scope outside Phase 5 claim/merge and Phase 17 notifications.

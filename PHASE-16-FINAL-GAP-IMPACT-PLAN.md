# Phase 16 final gap closure — decision-to-code map

2026-10-24. Business scope: the two owner-supplied final closure briefs.
Starting/main SHA: `898526bd9de4bf28bea9fa47d26b9ff7374d49e0`.
Branch: `codex/phase16-final-gap-closure`. Remote main fetched and matched.
The untracked API dotnet-tools.json is pre-existing and excluded from this work.

| Decision | Baseline finding | Implementation |
|---|---|---|
| BR-01–04 | Global Patient, patient-safe reads, exact treatment scope and source-doctor Hidden grants exist | Reuse; regression tests |
| BR-05–07 | Membership verification proves roles individually; it does not prove every adult-child pair | Add exact persisted parent verification and request types 3/4; preserve values 1/2 and all routes |
| BR-06 | All adult roles currently require eligibility | Parents use active exact proof, independent of historical eligibility state |
| BR-08 | Eligibility checks evidence for separate memberships | Bind Guardian eligibility to one approved exact authority request; support audited re-review |
| BR-09–10 | Safe dependent projection, policy timezone age cutoff and media rechecks exist | Reuse centrally; add proof to access version |
| BR-11 | Restriction/restoration and histories exist | Preserve; require explicitly configured grounds and current evidence for new restriction approval |
| BR-12–13 | Safe access log and separate Patient identity exist | Reuse; no identity deletion/merge changes |
| BR-14 | Claim retention and operational archive are separate domains | Document clinical retention/hold as deferred Phase 18 work; no purge |
| BR-15 | Signed policy gates, DB/JWT permissions, audit and disabled release flags exist | Preserve; no invented approval, jurisdiction or evidence taxonomy |

Migration: additive parent verification table with restrictive FKs, checks,
filtered uniqueness and rowversion; nullable Guardian authority request FK.
No historical migration edits, data deletion, guessed backfill or new permission.
Reconciliation is explicit, repeatable, exact-pair only and reports unresolved proof.
Rollback disables sharing and retains schema/history; schema removal is prohibited.

Baseline verification: first solution invocation encountered the host's inherited
`Platform=MCD`; rerun explicitly selects `Any CPU`. Test results are recorded in
the final evidence report. SQL Express is available locally; only disposable test
databases may be initialized. The authoritative full Master v1.2 is absent from
the repository; publish a self-contained dated incorporation addendum without
claiming formal incorporation, legal approval or deployment authorization.

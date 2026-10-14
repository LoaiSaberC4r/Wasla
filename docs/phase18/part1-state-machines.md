# Part One state machines

Every new write is an application transactional command. All existing-resource writes require latest RowVersion, the relevant current database permission/scope, an Idempotency-Key and any operation-specific reason. AvailableActions is a hint and is revalidated by the server. History is immutable; no delete endpoints exist.

| Aggregate | From → to | Preconditions / effect |
|---|---|---|
| Salary plan | create → Active; Active → Ended | Same Doctor/Reception/assignment, valid full-month terms and allocations; end reason; Ended terminal |
| Recurring plan | create → Active; Active → Paused; Paused → Active; Active/Paused → Ended | Valid lifecycle reason; immutable lifecycle revision; Ended terminal |
| Financial plan revision | Active/Paused → same lifecycle | Prospective terms; reason; latest version; existing obligations unchanged |
| Obligation | create/generate → Unpaid | Immutable amount/percentage/minor-unit snapshots; Variable may be unconfirmed |
| Obligation | Unpaid → Unpaid | Confirm actual or explicitly correct terms/amount with revision and reason |
| Obligation | Unpaid → Cancelled | Reason; immutable cancellation revision; cannot settle Cancelled |
| Obligation | Unpaid → Paid | Confirmed actual amount; exact full settlement; one active settlement |
| Obligation | Paid → Unpaid | Exactly one full compensating reversal of active settlement; reason; original remains immutable |
| Insight | calculated New → persisted Acknowledged | Current owned Practice, valid actual evidence; create evidence snapshot only on tracking |
| Insight | New → Dismissed | Mandatory reason; immutable evidence/history |
| Insight | Acknowledged → Resolved | Latest version; reason/follow-up; terminal Resolved |
| Handover | create → Open | Same Doctor/Practice and optional same-Practice reservation/ticket/previous-handover links; restricted operational summary |
| Handover | Open → Acknowledged | Authorized actor different from creator; record acknowledgement actor/time |
| Handover | Acknowledged → Closed | Authorized Reception or Doctor; mandatory operational outcome |
| Handover | Open → Closed | Doctor oversight permission; mandatory outcome/reason |
| Handover | Open/Acknowledged → same state | Authorized annotation append with version; no original-summary overwrite |
| Handover | Closed → — | Terminal; follow-up is a new linked handover |
| Investigation | create → Open | Approved administrative purpose, safe summary and authorized allow-listed link |
| Investigation | Open/InReview → same state | Assign to active nondeleted SuperAdmin account with actual SuperAdmin role; audited assignment |
| Investigation | Open → InReview | Active assigned SuperAdmin and StartReview permission |
| Investigation | InReview → Resolved | Active assignee, resolution outcome and notes |
| Investigation | Resolved → Closed | Closure notes and permission; all resolution facts retained in history |
| Investigation | Open → Closed | Explicit withdrawal reason; may be unassigned |
| Investigation | Resolved/Closed → InReview | Reopen reason and active assignee; optional replacement requires Assign permission; old resolution/history preserved |

No implicit start-review occurs on assignment, no implicit close occurs on resolution, and no implicit reopen occurs on editing. An inactive assignee prevents review/resolution until authorized reassignment. Urgent priority supplies no SLA, notification or escalation side effect. Cases never authorize private clinical or financial access.

Operational summary fields are normalized, bounded, HTML encoded, reject common clinical/PII/payroll/link patterns and control characters. This preventive filter is not a medical-content classifier; UI and operating policy still require nonclinical operational descriptions. Clinical attachments/results cannot be linked to cases or handovers.

Database ownership links, restrictive foreign keys, row versions, unique occurrences/effective settlements/reversals and immutable-history triggers enforce these workflows alongside domain validation. See `Phase18HandoverTests`, `Phase18InvestigationTests`, finance tests and corresponding API tests for permitted/forbidden transitions.

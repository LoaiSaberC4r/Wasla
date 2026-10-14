# Proposed Source of Truth addendum — Phase 18 Part One

This document records the owner's already-approved Phase18 Part One implementation decisions. It supplements the supplied v1.2 master and does not replace it. The filename/header discrepancy and source hash are recorded in the decisions document. No material conflict was identified in the reviewed rules.

## Existing authoritative rules retained

- Doctor ownership and Reception assignment/delegation define operational scope; global patient identity does not authorize cross-Doctor clinical access.
- Reservation, Ticket/queue and clinical encounter are separate aggregates. FollowUp is a VisitType. Queue ordering, patient collection/refund/revenue and existing booking commands retain their approved semantics.
- Doctor approval, public eligibility and actual bookability remain separate governance/availability concerns. Founder protection and first-login restrictions remain in existing commands/policies.
- Patient self clinical access, family entitlement/age/restriction rules, access logging, attachment reauthorization and disabled sharing gates remain under existing Phase13–16 policies.
- Existing appsettings keys/values are preserved by explicit owner requirement; exposed credentials require separate release remediation.

## Approved Phase18 additions

1. Doctor, Reception, SuperAdmin and Patient dashboards use real authoritative data with explicit scope, date basis, AsOfUtc and quality. No fabricated history or usage events.
2. Operating finance is distinct from patient collections: EGP, full salary calendar months/no proration, due-day clamping, owned/assigned allocations, deterministic minor-unit rounding, one-time and recurring liabilities, fixed/variable actual confirmation, full settlements and immutable compensating reversals. No bank transfer or formal payroll entitlement calculation.
3. Historical reporting preserves actual financial revisions/allocation snapshots and recorded-time cutoffs. Effective business date and recorded UTC knowledge are separate.
4. Doctor operational insights are deterministic source-backed signals, compare two complete7-day windows with≥20 valid samples and≥25% relative deterioration, and become persistent follow-up records only when tracked. No clinical advice, notification or automatic queue/payment action.
5. Practice handovers contain operational summaries and authorized same-Practice links, require different-creator acknowledgement, permit Doctor reasoned direct close, and retain immutable history. Closed is terminal.
6. Administrative investigations contain approved metadata purposes only. Assignment, review, resolution, closure and reasoned reopen are explicit versioned actions; no case grants clinical/private-financial access.
7. PDF/Excel exports share the preview model and separate permissions, enforce row/date/byte/runtime bounds, use installed sandboxed Chromium and ClosedXML, support bilingual/RTL presentation and record safe immutable export metadata.
8. Policy snapshots capture only observed configuration and support measured reconciliation/job health. Missing past policies/quotas remain NotMeasured, and absent login/page-view/search telemetry remains NotInstrumented.
9. Platform patient/clinical slices below five and complementary totals are suppressed. Patient dashboards compose only permitted self APIs and report disabled/unavailable capabilities explicitly.

## Still outside this approval

Part Two archive/retention/purge/Audit Explorer; subscription/commission; notification expansion; payroll taxes/advances/deductions/partial settlement; shift/attendance entitlements; formal operational SLAs; clinical scoring or AI recommendations. Medical/legal/retention/security-production and export-runtime approvals remain in `P18_PART1_EXTERNAL_RELEASE_BLOCKERS.md`. No implementation artifact constitutes production release approval.

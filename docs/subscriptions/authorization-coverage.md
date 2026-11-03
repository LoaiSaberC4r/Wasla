# PR #36 authorization entry-point audit

The explicit inventory contains all 335 stable permissions: 137 commercial permissions, 24 universal Patient permissions, 114 platform/catalog governance permissions, 19 account/onboarding permissions, 39 privacy/relationship permissions, and two audit/system permissions. Three commercial names are shared with Patient workflows; the Patient actor retains its universal policy and bypasses Doctor commercial prerequisites. Exact constants, GUIDs, access levels, actor types, original PR #36 classifications, and source references are in [permission-coverage.csv](permission-coverage.csv).

Unknown names have no implicit prefix classification. The coverage test compares the inventory against every member of `PermissionNames.All`, so a future permission needs deliberate classification. For Doctor/Reception commercial decisions, the stored mapping must match the reviewed feature/access rule and the accepted feature snapshot must provide the required access. Missing or inconsistent metadata denies access. Existing role, scope, privacy, user activity, and governance requirements remain mandatory.

| Operation / existing feature | Enforcement proof |
|---|---|
| Account, onboarding, Doctor profile | Explicit reviewed account permissions; existing profile/onboarding guards and approval state; operational writes still require their own feature and capacity |
| Practices / `PRACTICE_MANAGEMENT` | MVC permission guard; practice ownership; direct subscription capacity check before creation |
| Branding / `PRACTICE_BRANDING` | MVC permission guard plus owned practice checks |
| Schedule / `SCHEDULE_AVAILABILITY` | MVC permission guard plus owned practice checks |
| Basic segments/pricing / `BASIC_SEGMENTS_PRICING` | MVC permission guard plus owned practice checks; Booking Only has its narrow booking-price operation |
| Priority/quota / `ADVANCED_SEGMENTS_PRIORITY_QUOTA` | Separate `DoctorPracticeCatalog` entitlement check when advanced values are created/changed, even though the operation shares a basic permission |
| Public discovery / `PUBLIC_DISCOVERY` | `PublicDiscoveryService.EligibleDoctorQuery` verifies effective period, approval, publication and accepted feature snapshots in SQL |
| Online booking / `ONLINE_BOOKING` | `ReservationApplicationService.CreateAsync` checks the target Doctor's effective accepted feature; patient rights do not create a Doctor operational bypass |
| Reservation reads/mutations / `RESERVATION_MANAGEMENT` | Shared reservation actor/practice checks and subscription guard; ReadOnly for reads, Full for mutations; actual-resource continuity where permitted |
| Reservation capability responses | `ResolveMutationCapabilitiesAsync` batches effective permission checks, then applies lifecycle/cutoff/restore/archive rules; Reception delegation remains separate |
| Archived Reservations | Exact archive mappings; `ReportingAccessGuard` validates the requested permission; `ArchiveQueries` applies historical coverage before paging; detail/replay paths prove the resource |
| Tickets and queue / `TICKET_QUEUE` | Shared `TicketAccessService`; CallNext checks the selected Ticket under the existing queue lock; no general historical mutation bypass |
| Ticket archives | Exact archive mappings; requested-permission validation; filtered collection; original Ticket ownership and creation-time proof for historical exceptions; closed-practice relationship rules remain mandatory |
| Check-in / walk-in / `CHECKIN_WALKIN` | Shared ticket/check-in workflows, permission guard and capacity/state checks; practice defaults/configuration enforce the feature separately |
| Reception / `RECEPTION_MANAGEMENT` | MVC permission guard; owner/assignment checks; direct capacity checks for creation/reactivation; account-state permission has no subscription-account exemption |
| Patient management / `PATIENT_MANAGEMENT` | Existing role/assignment/relationship checks plus commercial permission authorization for Doctor/Reception; Patient identity and medical rights remain separate |
| Patient payments / `PAYMENT_COLLECTION` | Shared finance access, financial mutation and ticket guards; patient payment/refund arithmetic is unchanged; continuity resolves the original committed resource |
| Doctor revenue / `DOCTOR_REVENUE_DASHBOARD` | Finance authorization and requested-permission validation in `ReportingAccessGuard`; Patient and platform finance policies remain separate |
| Encounters / `MEDICAL_ENCOUNTER` | `ClinicalAccessService` and existing owned historical/relationship policies; existing mutation workflows check actual encounter continuity |
| Diagnosis / `DIAGNOSIS` | Clinical guards and diagnosis permission checks; no plan can grant patient identity, consent or cross-Doctor access |
| Prescription / `PRESCRIPTION` | Shared `MedicationAccess` and owned source/relationship policies; actual mutation resource passed for continuity |
| Lab/radiology / `LAB_RADIOLOGY` | Shared `DiagnosticAccess`, source ownership/relationship checks and attachment privacy checks; Patient submissions and own results remain universal |
| Follow-up / `FOLLOWUP_ELIGIBILITY` | Shared clinical/booking authorization plus existing eligibility rules; the shared Patient booking-eligibility permission remains universal for Patient actors |
| Treating Doctor medical record / `MEDICAL_RECORD_ACCESS` | `MedicalRecordAccessPolicy`, current-treatment proof, sharing configuration, relationship, consent, hidden-entry and family policies; Patient/Guardian own rights are separate |
| Handover / `RECEPTION_HANDOVER` | `HandoverService` uses the shared reporting guard for each requested view/mutation; Reception assignment remains mandatory |
| Expenses / `OPERATING_EXPENSES` | `OperatingFinanceService` and requested-permission validation in `ReportingAccessGuard` |
| Salaries / `SALARY_MANAGEMENT` | Operating finance guards plus owner/reception validation and existing transactional commands |
| Reports / `OPERATIONAL_REPORTS` | `ReportingQueryService` and shared requested-permission guard; downstream scope permissions are filtered to the same effective authorization |
| Exports / `PDF_XLSX_EXPORT` | Report/export guards and release-time revalidation; prescription export has an exact export-feature mapping rather than inheriting Prescription |
| Cross-practice comparison / `CROSS_PRACTICE_COMPARISON` | Separate entitlement check in `ReportingQueryService.BuildAsync`; the shared Reports permission alone is insufficient |
| Insights / `ADVANCED_RULE_BASED_INSIGHTS` | `DoctorInsightService` / insight evidence readers use the shared requested-permission guard; no Professional Reports bypass |
| Audit metadata | Existing audit/own-activity/sensitive-access rights remain mandatory. Operational Ticket/Reservation sources and identifiers are filtered through accepted read scopes; handover/export/finance metadata uses its own feature coverage |
| Patient/Guardian archives and medical records | Existing patient ownership, dependent-family and legal/privacy policies; no Doctor subscription prerequisite |
| SuperAdmin / catalog managers | Existing strict actor, role, permission, root-only and governance checks; no Doctor subscription prerequisite |

No new permission, feature, permission GUID, plan entitlement, commercial price, capacity, trial rule, schema, migration, production deployment, or global patient-data cache was introduced. Catalog reseeding inserts four archive mappings, repairs the prescription export mapping, and removes 51 obsolete prefix-derived mappings for explicitly noncommercial permissions. It never changes accepted subscription terms or relational feature snapshots.

# Required regression coverage map

This maps the owner's T01–T38 requirements to executable coverage. It does not
count a skipped test as a pass. Exact executed outcomes and checkout SHA belong
to TEST-EVIDENCE.json and the final-head CI test-results artifact.

| ID | Executable coverage |
|---|---|
| T01–T04 | FamilySelfServiceApiTests and FamilyClosureRegressionTests: zero/one/bounded multiple; optional/shared phone; wrong actor, expiry, replay, stale identity; paged explicit existing-identity review |
| T05–T09 | FamilyClosureRegressionTests, FamilyGovernanceApiTests, FamilySelfServiceApiTests: confidential cross-family intake, four contact combinations, existing identity reuse, SQL concurrent approval and parent-role contention |
| T10–T14 | FamilyClosureRegressionTests, Phase13LifecycleSecurityTests, DependentArchivedTicketTests: bookable child, create/list/details/reschedule/cancel, valid follow-up, linked ticket, archive/permission and live revocation |
| T15–T16 | FamilySelfServiceApiTests and FamilyClosureRegressionTests: empty legacy404/new Workspace200, safe pending/conflict summaries, independent reasons and bounded one-versus-thirteen-child reader count |
| T17–T19 | FamilySelfServiceApiTests and DependentProfileClosureTests: primary fallback, rowversion/idempotency, photo DB failure/old-file retention/retry, immutable before/after audit and phone update |
| T20–T22 | DependentFinanceClosureTests and FamilyClosureRegressionTests: actual child payment/refund receipts, transaction scope, cross-child404 and independent receipt-grant403 |
| T23–T24 | ProductionDependencyTests, ClamAvMalwareScannerTests, HistoricalValidationLifecycleTests, HistoricalDocumentFailureTests: real clean/EICAR, unavailable/timeout, durable rejected/quarantined verdicts, SQL rollback and lost acknowledgement |
| T25–T26 | PrivateMediaContinuationTests, DependentProfileClosureTests, HistoricalDocumentFailureTests: six storage modules, missing references, orphan candidates, new-context durable continuation and no deletion |
| T27 | ProductionDependencyTests: certificate-protected XML key ring, independent provider instances/restart, actual invitation protector purpose and wrong-purpose denial |
| T28–T29 | HistoricalDocumentApiTests and HistoricalValidationLifecycleTests: version/withdraw/timeline pagination and unchanged official clinical result counts |
| T30 | FamilyMigrationRehearsalTests: actual generated idempotent script on clean, populated and backup-restored native SQL Server; retained IDs and replay |
| T31 | FamilySelfServiceFoundationTests, CommercialFeaturePermissionCoverageTests, FamilySelfServiceApiTests security-seed replay: frozen371 IDs, code inventory and additive seeding |
| T32 | ProductionActivationTests: effective enabled flags, exact policy, forbidden startup migration/seeding and required real host paths; startup scanner/SMTP/protected-key checks |
| T33–T34 | Phase16 Medical Record/family suites and Phase16FinalGapTests: treating/cross-doctor/hidden scopes, exact medical proof, other-child denial, restrictions and live post-open reauthorization |
| T35–T36 | DependentBirthdayBoundaryTests: native SQL and SQLite variants at Cairo's exact sixteenth/eighteenth birthday boundaries |
| T37 | Phase17RecipientNotificationTests and notification privacy/eligibility suites: self-only clinical disclosure and independent operational proof/recipient checks |
| T38 | EmailOutboxTests, Phase17SmtpBoundaryTests, EmailOutboxWorkerTests: durable queue idempotency, retries/leases/failure state, worker processing and no resend after Sent |

Real SMTP connectivity/STARTTLS/authentication is tested separately by
Wasla.ProductionProbe. Authentication was rejected, so no controlled message was
sent and recipient inbox delivery is unverified. Synthetic test senders are never
reported as actual SMTP delivery. Real ClamAV/EICAR verification is separate from
the deterministic ClamD verdict fixture used to test persistence transitions.

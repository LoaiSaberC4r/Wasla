# Phase 18 Part One rollout

Production release is blocked by the existing exposed-credential issue until independently remediated. Existing appsettings remains byte-for-byte unchanged by instruction. Obtain approved environment/secret overrides and deployment authorization separately; this implementation neither deploys nor rotates credentials.

1. Review `P18_PART1_IMPLEMENTATION_REPORT.md`, source authority/addendum, API/UI contracts, SQL migration script and external blockers. Back up and rehearse against a nonproduction copy with the target SQL Server version. Verify current migration history. Do not assume local SQL Express validates production load or hosting security.
2. Build with the pinned .NET10 SDK/packages, run unit/architecture/integration with isolated SQL test databases and inspect zero-warning build/model drift. Preserve Phase10–16 regression outcomes, especially migration legacy visibility/family privacy.
3. Provision a patched sandbox-capable Chromium and licensed system Arabic fonts. Supply `Phase18__ChromiumExecutablePath` via deployment environment/secret configuration. Permit only intended executable access and private logo reads; egress must remain restricted. Verify actual runtime probe and Arabic/English A4 PDFs. XLSX works independently of Chromium.
4. Keep new workers disabled through environment overrides during migration/rehearsal. Apply the reviewed additive schema and normal idempotent security seed. No automatic Reception assignment grants are added. Existing disabled clinical-sharing settings retain their approved values.
5. Start application, verify database initialization completes before generation/refresh/probe hosted services. Enable measured policy refresh; initial snapshots reflect current observation time only. Expect historical capacity to remain NotMeasured until trustworthy coverage exists.
6. Enable obligation generation after checking sampled plans/intervals/allocations/Reception assignments. Current eligible cycle may create Upcoming obligations before its due day; at most24 new occurrences per plan per pass. Verify no duplicate occurrences or automatic settlement, and that restarts continue catch-up safely.
7. Smoke-test one scoped Doctor finance lifecycle, each role's dashboard/catalog/options, Reception explicit delegation/revocation and handover, admin investigation, self Patient composition, PDF/XLSX and controlled render failure. Use synthetic nonproduction data. Refresh detail/history/report and reconcile allocation totals/cash versus due.
8. Monitor real generation/refresh runs, stale thresholds, failure codes, outbox counts and export audit/runtime health. No new SMS/email notification is introduced. Investigate discrepancies through authorized detail/history; never rewrite immutable evidence.

## Supported environment overrides

| Environment key | Default | Valid range / effect |
|---|---|---|
| Phase18__DefaultReportTimeZoneId | Africa/Cairo | Valid installed time-zone ID |
| Phase18__ObligationGenerationEnabled | true | Enable/disable future worker passes |
| Phase18__ObligationGenerationIntervalSeconds | 300 | 30–86400 |
| Phase18__ReportingPolicyRefreshEnabled | true | Enable/disable current-policy reconciliation |
| Phase18__ReportingPolicyRefreshIntervalSeconds | 300 | 30–86400 |
| Phase18__ChromiumExecutablePath | absent | Installed reviewed executable; absence means PDF controlled503 |
| Phase18__PdfTimeoutSeconds | 45 | 5–120 |
| Phase18__MaximumConcurrentPdfRenders | 2 | 1–4 |

No secret, connection string or original appsettings content is reproduced here. Existing deployment startup/seeding switches remain existing operational controls. Test-only `WASLA_SQLSERVER_CONNECTION_STRING`, `WASLA_CHROMIUM_PATH`, `WASLA_EXPORT_QA_DIRECTORY`, `WASLA_CONTRACT_OUTPUT_DIRECTORY` are test harness inputs, not the production Phase18 option binding.

## Rollback and approvals

Disable workers and new traffic first. Preserve all liabilities, immutable movements/history, policy evidence and export audits. Use the approved application/database restore plan. Down migrations destroy new phase data and are not routine production rollback; reverting nullable system actors explicitly refuses to fabricate a replacement actor. No automatic purge, archive, retention or clinical gate change is authorized.

Required independent release closure: exposed credentials/history remediation; medical/legal/privacy approval for any sharing activation; approved retention policy before PartTwo; production browser sandbox/font/runtime review; environment-specific capacity/load rehearsal. Local artifacts/tests establish implementation evidence and do not imply these approvals.

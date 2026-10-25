param(
    [string]$ResultsDirectory = (Join-Path $PSScriptRoot '../../TestResults/phase17/regression'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../../docs/phase17'),
    [ValidateSet('NOT RUN', 'PASS', 'FAIL', 'IN PROGRESS')][string]$CiResult = 'NOT RUN',
    [string]$CiRunUrl = '',
    [string]$CiCommitSha = ''
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$resultsRoot = [IO.Path]::GetFullPath($ResultsDirectory)
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
[void][IO.Directory]::CreateDirectory($outputRoot)

# Each case names executable tests. Missing/skipped tests never become PASS.
$aliases = @{
    N = 'Wasla.Tests.Integration.Phase17NotificationTests.'
    L = 'Wasla.Tests.Integration.Phase17LifecycleNotificationTests.'
    C = 'Wasla.Tests.Integration.Phase17ClinicalNotificationTests.'
    R = 'Wasla.Tests.Integration.Phase17RecipientNotificationTests.'
    G = 'Wasla.Tests.Integration.Phase17GovernanceNotificationTests.'
    S = 'Wasla.Tests.Integration.Phase17NotificationSqlServerTests.'
    M = 'Wasla.Tests.Integration.Phase17NotificationMigrationTests.'
    T = 'Wasla.Tests.Integration.Phase17SmtpBoundaryTests.'
    Q = 'Wasla.Tests.Integration.Phase17ReliabilityNotificationTests.'
    U = 'Wasla.Tests.Unit.Phase17NotificationTests.'
    A = 'Wasla.Tests.Architecture.Phase17NotificationArchitectureTests.'
    F = 'Wasla.Tests.Integration.TrustAccessApiTests.'
    D = 'Wasla.Tests.Integration.DoctorOnboardingApiTests.'
    E = 'Wasla.Tests.Integration.EmailOutboxTests.'
    H = 'Wasla.Tests.Integration.Phase18PartTwoSqlTests.'
}
$mapping = @'
A01|N:Capture_schedules_only_strictly_future_due_times
A02|N:Capture_schedules_only_strictly_future_due_times
A03|N:Capture_schedules_only_strictly_future_due_times
A04|N:Dispatcher_obeys_due_grace_and_appointment_boundaries
A05|N:Dispatcher_obeys_due_grace_and_appointment_boundaries
A06|N:Dispatcher_obeys_due_grace_and_appointment_boundaries
A07|N:Dispatcher_obeys_due_grace_and_appointment_boundaries
A08|N:A08_A09_A11_A12_mutations_preserve_history_and_invalidate_previous_revisions
A09|N:A08_A09_A11_A12_mutations_preserve_history_and_invalidate_previous_revisions,S:O03_source_commit_serializes_with_dispatch_and_invalidates_old_revision
A10|L:A10_new_reschedule_can_have_its_own_reminder_without_retracting_prior_delivery
A11|N:A08_A09_A11_A12_mutations_preserve_history_and_invalidate_previous_revisions
A12|N:A08_A09_A11_A12_mutations_preserve_history_and_invalidate_previous_revisions
A13|N:Restore_keeps_revision_and_never_creates_a_second_reminder
A14|N:Restore_keeps_revision_and_never_creates_a_second_reminder,L:A14_O15_legacy_nullable_revision_can_restore_without_backfill_or_invented_revision
A15|N:Send_time_guard_suppresses_expired_or_cancelled_reminders_before_smtp,L:E02_NoShow_and_expiration_capture_only_inapp_and_do_not_change_source_lifecycle,L:A16_E03_successful_normal_and_force_checkin_capture_one_attendance_update
A16|L:A16_E03_successful_normal_and_force_checkin_capture_one_attendance_update
A17|L:A17_O18_walkin_and_feature_disable_never_create_historical_reminders
A18|L:A18_platform_setting_audit_keeps_existing_due_snapshot,S:A18_concurrent_settings_and_preference_updates_return_one_conflict
A19|N:A19_O09_C12_api_permissions_admin_metadata_and_denial_audit_are_enforced
A20|L:A20_A21_timezone_configuration_changes_do_not_recompute_committed_utc_or_reminder_snapshots
A21|L:A20_A21_timezone_configuration_changes_do_not_recompute_committed_utc_or_reminder_snapshots
A22|L:A22_closed_day_suppresses_reminder_without_mutating_no_show_or_expiration_lifecycle
R01|R:R01_R04_R12_trust_survives_otp_cleanup_but_does_not_follow_email_reassignment_or_account_closure,N:R03_R04_R13_E01_E05_no_trusted_email_still_commits_and_delivers_only_own_inboxes
R02|R:R02_verified_reviewed_global_contact_gets_email_without_an_invented_inbox
R03|N:R03_R04_R13_E01_E05_no_trusted_email_still_commits_and_delivers_only_own_inboxes
R04|R:R01_R04_R12_trust_survives_otp_cleanup_but_does_not_follow_email_reassignment_or_account_closure,N:R03_R04_R13_E01_E05_no_trusted_email_still_commits_and_delivers_only_own_inboxes
R05|L:R05_E01_O01_controlled_email_cutover_has_one_owner_and_replay_does_not_recapture,G:E06_governance_keeps_existing_email_and_adds_only_eligible_own_inbox
R06|R:R06_R07_R09_R11_family_membership_and_current_pairwise_authority_are_distinct
R07|R:R06_R07_R09_R11_family_membership_and_current_pairwise_authority_are_distinct
R08|R:R08_revoked_guardian_authority_prevents_dependent_reminder_delivery
R09|R:R06_R07_R09_R11_family_membership_and_current_pairwise_authority_are_distinct
R10|U:R10_R11_logical_audiences_keep_distinct_inboxes_and_missing_owner_can_only_be_suppressed,R:R06_R07_R09_R11_family_membership_and_current_pairwise_authority_are_distinct
R11|U:R10_R11_logical_audiences_keep_distinct_inboxes_and_missing_owner_can_only_be_suppressed,R:R06_R07_R09_R11_family_membership_and_current_pairwise_authority_are_distinct
R12|R:R01_R04_R12_trust_survives_otp_cleanup_but_does_not_follow_email_reassignment_or_account_closure
R13|N:R03_R04_R13_E01_E05_no_trusted_email_still_commits_and_delivers_only_own_inboxes
R14|R:R14_deactivated_reception_assignment_denies_operational_inbox_access,D:OperationalPracticeApi_EnforcesOwnershipLifecycleSchedulingPricingAndReceptionScope
R15|N:R03_R04_R13_E01_E05_no_trusted_email_still_commits_and_delivers_only_own_inboxes,D:OperationalPracticeApi_EnforcesOwnershipLifecycleSchedulingPricingAndReceptionScope
R16|C:C09_R16_current_family_booking_authority_and_parent_optin_do_not_create_dependent_clinical_delivery
E01|L:R05_E01_O01_controlled_email_cutover_has_one_owner_and_replay_does_not_recapture,N:A08_A09_A11_A12_mutations_preserve_history_and_invalidate_previous_revisions
E02|L:E02_NoShow_and_expiration_capture_only_inapp_and_do_not_change_source_lifecycle
E03|L:A16_E03_successful_normal_and_force_checkin_capture_one_attendance_update
E04|U:E04_E08_unapproved_triggers_cannot_be_captured,A:E04_E07_E08_C11_O14_contracts_have_no_arbitrary_send_or_clinical_payload_surface
E05|N:R03_R04_R13_E01_E05_no_trusted_email_still_commits_and_delivers_only_own_inboxes
E06|G:E06_governance_keeps_existing_email_and_adds_only_eligible_own_inbox
E07|A:E04_E07_E08_C11_O14_contracts_have_no_arbitrary_send_or_clinical_payload_surface,D:OperationalPracticeApi_EnforcesOwnershipLifecycleSchedulingPricingAndReceptionScope
E08|U:E04_E08_unapproved_triggers_cannot_be_captured
E09|N:E09_E10_preferences_are_concurrent_independent_and_checked_at_dispatch
E10|N:E09_E10_preferences_are_concurrent_independent_and_checked_at_dispatch
E11|F:PasswordRecovery_HashesProofsAndConsumesChallengeOnce,U:E09_E10_E11_E12_preferences_are_independent_and_essential_categories_are_not_editable
E12|C:C03_C04_C05_C06_C07_C08_C09_C11_E12_E13_diagnostic_transitions_are_durable_generic_and_self_only
E13|C:C03_C04_C05_C06_C07_C08_C09_C11_E12_E13_diagnostic_transitions_are_durable_generic_and_self_only
E14|U:E14_E15_E16_templates_fallback_version_and_encode_only_safe_booking_fields
E15|U:E14_E15_E16_templates_fallback_version_and_encode_only_safe_booking_fields
E16|U:E14_E15_E16_templates_fallback_version_and_encode_only_safe_booking_fields
E17|U:Grace_window_is_inclusive_and_has_no_quiet_hours
C01|C:C01_C02_C10_prescription_draft_finalize_hidden_navigation_and_source_reauthorization
C02|C:C01_C02_C10_prescription_draft_finalize_hidden_navigation_and_source_reauthorization
C03|C:C03_C04_C05_C06_C07_C08_C09_C11_E12_E13_diagnostic_transitions_are_durable_generic_and_self_only
C04|C:C03_C04_C05_C06_C07_C08_C09_C11_E12_E13_diagnostic_transitions_are_durable_generic_and_self_only
C05|C:C03_C04_C05_C06_C07_C08_C09_C11_E12_E13_diagnostic_transitions_are_durable_generic_and_self_only
C06|C:C03_C04_C05_C06_C07_C08_C09_C11_E12_E13_diagnostic_transitions_are_durable_generic_and_self_only
C07|C:C03_C04_C05_C06_C07_C08_C09_C11_E12_E13_diagnostic_transitions_are_durable_generic_and_self_only
C08|C:C01_C02_C10_prescription_draft_finalize_hidden_navigation_and_source_reauthorization,C:C03_C04_C05_C06_C07_C08_C09_C11_E12_E13_diagnostic_transitions_are_durable_generic_and_self_only
C09|C:C09_R16_current_family_booking_authority_and_parent_optin_do_not_create_dependent_clinical_delivery
C10|C:C01_C02_C10_prescription_draft_finalize_hidden_navigation_and_source_reauthorization
C11|A:E04_E07_E08_C11_O14_contracts_have_no_arbitrary_send_or_clinical_payload_surface,C:C03_C04_C05_C06_C07_C08_C09_C11_E12_E13_diagnostic_transitions_are_durable_generic_and_self_only
C12|N:A19_O09_C12_api_permissions_admin_metadata_and_denial_audit_are_enforced,S:O10_admin_retry_is_atomic_audited_and_rejects_already_claimed_work,A:E04_E07_E08_C11_O14_contracts_have_no_arbitrary_send_or_clinical_payload_surface
O01|L:R05_E01_O01_controlled_email_cutover_has_one_owner_and_replay_does_not_recapture,U:O01_O11_terminal_intents_and_idempotent_reads_preserve_logical_identity
O02|S:O02_two_real_sql_workers_create_one_logical_delivery_per_channel
O03|S:O03_source_commit_serializes_with_dispatch_and_invalidates_old_revision,T:O03_O04_smtp_admission_serializes_source_commit_without_holding_sql_transaction,N:Send_time_guard_suppresses_expired_or_cancelled_reminders_before_smtp
O04|Q:O04_provider_acceptance_before_an_unobserved_ack_can_duplicate_physical_delivery_but_not_logical_identity,T:O03_O04_smtp_admission_serializes_source_commit_without_holding_sql_transaction
O05|S:O05_O08_smtp_failure_retries_independently_and_expired_reminder_is_suppressed,E:TransientFailure_SchedulesRetry_WithoutDeletingMessage
O06|Q:O06_O08_permanent_authentication_failure_is_safe_and_stale_admin_retry_is_denied
O07|S:O07_expired_claim_is_recovered_once_and_unique_delivery_is_enforced_by_sql,E:ProcessingLease_PreventsEarlyClaimAndAllowsRecoveryAfterExpiry
O08|Q:O06_O08_permanent_authentication_failure_is_safe_and_stale_admin_retry_is_denied,S:O05_O08_smtp_failure_retries_independently_and_expired_reminder_is_suppressed,N:Send_time_guard_suppresses_expired_or_cancelled_reminders_before_smtp,S:O10_admin_retry_is_atomic_audited_and_rejects_already_claimed_work
O09|N:A19_O09_C12_api_permissions_admin_metadata_and_denial_audit_are_enforced,S:O10_admin_retry_is_atomic_audited_and_rejects_already_claimed_work
O10|S:O10_admin_retry_is_atomic_audited_and_rejects_already_claimed_work
O11|N:O11_O12_inbox_pagination_counts_and_idempotent_read_are_owner_scoped
O12|Q:O12_large_inbox_has_bounded_stable_pages_counts_and_own_bulk_marking
O13|H:Linked_group_rolls_back_each_database_step_then_replays_and_refunds_without_unarchiving,H:Closed_noshow_keeps_status_releases_patient_lease_and_missing_payment_is_quarantined
O14|A:E04_E07_E08_C11_O14_contracts_have_no_arbitrary_send_or_clinical_payload_surface,U:E14_E15_E16_templates_fallback_version_and_encode_only_safe_booking_fields
O15|M:O15_model_snapshot_matches_and_migrations_are_additive_without_backfill,M:O15_populated_legacy_outbox_survives_native_sql_upgrade_without_resend_or_backfill
O16|A:E04_E07_E08_C11_O14_contracts_have_no_arbitrary_send_or_clinical_payload_surface
O17|A:E04_E07_E08_C11_O14_contracts_have_no_arbitrary_send_or_clinical_payload_surface
O18|L:A17_O18_walkin_and_feature_disable_never_create_historical_reminders,S:O07_expired_claim_is_recovered_once_and_unique_delivery_is_enforced_by_sql
'@

$runs = @()
$latestTests = @{}
$documents = foreach ($file in @(Get-ChildItem -LiteralPath $resultsRoot -Filter '*.trx')) {
    [xml]$document = [IO.File]::ReadAllText($file.FullName)
    [pscustomobject]@{ File = $file; Document = $document; StartedAt = [DateTimeOffset]::Parse([string]$document.TestRun.Times.start, [Globalization.CultureInfo]::InvariantCulture) }
}
# A long-running old build can finish after a focused new-build rerun. Prefer the later
# invocation, rather than letting file completion time replace stronger/newer assertions.
foreach ($entry in @($documents | Sort-Object StartedAt)) {
    $file = $entry.File; $document = $entry.Document
    $counters = $document.TestRun.ResultSummary.Counters
    $finishedAt = [DateTimeOffset]::Parse([string]$document.TestRun.Times.finish, [Globalization.CultureInfo]::InvariantCulture)
    $runs += [ordered]@{ File = $file.Name; StartedAtUtc = $entry.StartedAt.ToUniversalTime().ToString('o'); FinishedAtUtc = $finishedAt.ToUniversalTime().ToString('o'); Duration = ($finishedAt - $entry.StartedAt).ToString('c'); Total = [int]$counters.total; Passed = [int]$counters.passed; Failed = [int]$counters.failed; NotExecuted = [int]$counters.notExecuted }
    foreach ($result in @($document.TestRun.Results.UnitTestResult)) {
        $latestTests[[string]$result.testName] = [ordered]@{ TestName = [string]$result.testName; Outcome = [string]$result.outcome; Duration = [string]$result.duration; RunFile = $file.Name }
    }
}
$cases = @()
foreach ($line in ($mapping.Trim() -split '\r?\n')) {
    $parts = $line.Split('|', 2)
    $prefixes = @($parts[1].Split(',') | ForEach-Object { $reference = $_.Split(':', 2); $aliases[$reference[0]] + $reference[1] })
    $matches = @(); $missing = @()
    foreach ($prefix in $prefixes) {
        $found = @($latestTests.Values | Where-Object { $_.TestName.StartsWith($prefix, [StringComparison]::Ordinal) })
        if ($found.Count -eq 0) { $missing += $prefix } else { $matches += $found }
    }
    $status = if ($missing.Count -gt 0 -or @($matches | Where-Object Outcome -eq 'NotExecuted').Count -gt 0) { 'NOT RUN' }
        elseif (@($matches | Where-Object Outcome -ne 'Passed').Count -gt 0) { 'FAIL' } else { 'PASS' }
    $cases += [ordered]@{ CaseId = $parts[0]; Result = $status; TestMethods = $prefixes; MissingTests = $missing; Evidence = @($matches | Sort-Object TestName -Unique) }
}
if ($cases.Count -ne 85 -or @($cases.CaseId | Sort-Object -Unique).Count -ne 85) { throw 'The acceptance map must contain all 85 unique case IDs.' }
$report = [ordered]@{
    GeneratedAtUtc = [DateTime]::UtcNow.ToString('o')
    Branch = (git -C $repositoryRoot branch --show-current)
    Baseline = '68902c076c4b46415ed90e406212ea6e9ee7c0a6'
    CiResult = $CiResult
    CiRunUrl = $CiRunUrl
    CiCommitSha = $CiCommitSha
    Runs = $runs
    UniqueExecutedTests = $latestTests.Count
    UniquePassedTests = @($latestTests.Values | Where-Object Outcome -eq 'Passed').Count
    UniqueFailedTests = @($latestTests.Values | Where-Object Outcome -eq 'Failed').Count
    UniqueNotExecutedTests = @($latestTests.Values | Where-Object Outcome -eq 'NotExecuted').Count
    AcceptancePassed = @($cases | Where-Object Result -eq 'PASS').Count
    AcceptanceFailed = @($cases | Where-Object Result -eq 'FAIL').Count
    AcceptanceNotRun = @($cases | Where-Object Result -eq 'NOT RUN').Count
    Cases = $cases
}
[IO.File]::WriteAllText((Join-Path $outputRoot 'TEST-EVIDENCE.json'), ($report | ConvertTo-Json -Depth 12) + [Environment]::NewLine)
$markdown = @('# Phase 17 acceptance evidence', '', 'Generated from executed TRX results. PASS requires every mapped method/data row to have executed successfully. Missing or skipped evidence is NOT RUN. Later reruns replace earlier outcomes for the same test name.', '', '| Case | Result | Executable test methods |', '|---|---|---|')
foreach ($case in $cases) { $markdown += '| ' + $case.CaseId + ' | ' + $case.Result + ' | ' + (($case.TestMethods | ForEach-Object { '`' + $_ + '`' }) -join '<br>') + ' |' }
$markdown += @('', 'Full data-row names, durations and originating run files are in TEST-EVIDENCE.json.', '', 'Shared-address coverage preserves recipient identity and excludes ineligible family audiences. Existing unique account-email rules are retained; this module does not create shared accounts or fabricate verified contacts.', '', 'The native SQL acceptance runs use real SQL Server and isolated test databases. Physical SMTP uncertainty uses a deterministic fake provider, including simulated acceptance with a lost response. No production SMTP delivery is asserted.')
[IO.File]::WriteAllLines((Join-Path $outputRoot 'ACCEPTANCE.md'), $markdown)
Write-Output ("Acceptance: {0}/85 PASS; {1} FAIL; {2} NOT RUN. Unique tests: {3}." -f $report.AcceptancePassed, $report.AcceptanceFailed, $report.AcceptanceNotRun, $report.UniqueExecutedTests)

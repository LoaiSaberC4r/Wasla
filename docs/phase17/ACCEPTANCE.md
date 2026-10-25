# Phase 17 acceptance evidence

Generated from executed TRX results. PASS requires every mapped method/data row to have executed successfully. Missing or skipped evidence is NOT RUN. Later reruns replace earlier outcomes for the same test name.

| Case | Result | Executable test methods |
|---|---|---|
| A01 | PASS | `Wasla.Tests.Integration.Phase17NotificationTests.Capture_schedules_only_strictly_future_due_times` |
| A02 | PASS | `Wasla.Tests.Integration.Phase17NotificationTests.Capture_schedules_only_strictly_future_due_times` |
| A03 | PASS | `Wasla.Tests.Integration.Phase17NotificationTests.Capture_schedules_only_strictly_future_due_times` |
| A04 | PASS | `Wasla.Tests.Integration.Phase17NotificationTests.Dispatcher_obeys_due_grace_and_appointment_boundaries` |
| A05 | PASS | `Wasla.Tests.Integration.Phase17NotificationTests.Dispatcher_obeys_due_grace_and_appointment_boundaries` |
| A06 | PASS | `Wasla.Tests.Integration.Phase17NotificationTests.Dispatcher_obeys_due_grace_and_appointment_boundaries` |
| A07 | PASS | `Wasla.Tests.Integration.Phase17NotificationTests.Dispatcher_obeys_due_grace_and_appointment_boundaries` |
| A08 | PASS | `Wasla.Tests.Integration.Phase17NotificationTests.A08_A09_A11_A12_mutations_preserve_history_and_invalidate_previous_revisions` |
| A09 | PASS | `Wasla.Tests.Integration.Phase17NotificationTests.A08_A09_A11_A12_mutations_preserve_history_and_invalidate_previous_revisions`<br>`Wasla.Tests.Integration.Phase17NotificationSqlServerTests.O03_source_commit_serializes_with_dispatch_and_invalidates_old_revision` |
| A10 | PASS | `Wasla.Tests.Integration.Phase17LifecycleNotificationTests.A10_new_reschedule_can_have_its_own_reminder_without_retracting_prior_delivery` |
| A11 | PASS | `Wasla.Tests.Integration.Phase17NotificationTests.A08_A09_A11_A12_mutations_preserve_history_and_invalidate_previous_revisions` |
| A12 | PASS | `Wasla.Tests.Integration.Phase17NotificationTests.A08_A09_A11_A12_mutations_preserve_history_and_invalidate_previous_revisions` |
| A13 | PASS | `Wasla.Tests.Integration.Phase17NotificationTests.Restore_keeps_revision_and_never_creates_a_second_reminder` |
| A14 | PASS | `Wasla.Tests.Integration.Phase17NotificationTests.Restore_keeps_revision_and_never_creates_a_second_reminder`<br>`Wasla.Tests.Integration.Phase17LifecycleNotificationTests.A14_O15_legacy_nullable_revision_can_restore_without_backfill_or_invented_revision` |
| A15 | PASS | `Wasla.Tests.Integration.Phase17NotificationTests.Send_time_guard_suppresses_expired_or_cancelled_reminders_before_smtp`<br>`Wasla.Tests.Integration.Phase17LifecycleNotificationTests.E02_NoShow_and_expiration_capture_only_inapp_and_do_not_change_source_lifecycle`<br>`Wasla.Tests.Integration.Phase17LifecycleNotificationTests.A16_E03_successful_normal_and_force_checkin_capture_one_attendance_update` |
| A16 | PASS | `Wasla.Tests.Integration.Phase17LifecycleNotificationTests.A16_E03_successful_normal_and_force_checkin_capture_one_attendance_update` |
| A17 | PASS | `Wasla.Tests.Integration.Phase17LifecycleNotificationTests.A17_O18_walkin_and_feature_disable_never_create_historical_reminders` |
| A18 | PASS | `Wasla.Tests.Integration.Phase17LifecycleNotificationTests.A18_platform_setting_audit_keeps_existing_due_snapshot`<br>`Wasla.Tests.Integration.Phase17NotificationSqlServerTests.A18_concurrent_settings_and_preference_updates_return_one_conflict` |
| A19 | PASS | `Wasla.Tests.Integration.Phase17NotificationTests.A19_O09_C12_api_permissions_admin_metadata_and_denial_audit_are_enforced` |
| A20 | PASS | `Wasla.Tests.Integration.Phase17LifecycleNotificationTests.A20_A21_timezone_configuration_changes_do_not_recompute_committed_utc_or_reminder_snapshots` |
| A21 | PASS | `Wasla.Tests.Integration.Phase17LifecycleNotificationTests.A20_A21_timezone_configuration_changes_do_not_recompute_committed_utc_or_reminder_snapshots` |
| A22 | PASS | `Wasla.Tests.Integration.Phase17LifecycleNotificationTests.A22_closed_day_suppresses_reminder_without_mutating_no_show_or_expiration_lifecycle` |
| R01 | PASS | `Wasla.Tests.Integration.Phase17RecipientNotificationTests.R01_R04_R12_trust_survives_otp_cleanup_but_does_not_follow_email_reassignment_or_account_closure`<br>`Wasla.Tests.Integration.Phase17NotificationTests.R03_R04_R13_E01_E05_no_trusted_email_still_commits_and_delivers_only_own_inboxes` |
| R02 | PASS | `Wasla.Tests.Integration.Phase17RecipientNotificationTests.R02_verified_reviewed_global_contact_gets_email_without_an_invented_inbox` |
| R03 | PASS | `Wasla.Tests.Integration.Phase17NotificationTests.R03_R04_R13_E01_E05_no_trusted_email_still_commits_and_delivers_only_own_inboxes` |
| R04 | PASS | `Wasla.Tests.Integration.Phase17RecipientNotificationTests.R01_R04_R12_trust_survives_otp_cleanup_but_does_not_follow_email_reassignment_or_account_closure`<br>`Wasla.Tests.Integration.Phase17NotificationTests.R03_R04_R13_E01_E05_no_trusted_email_still_commits_and_delivers_only_own_inboxes` |
| R05 | PASS | `Wasla.Tests.Integration.Phase17LifecycleNotificationTests.R05_E01_O01_controlled_email_cutover_has_one_owner_and_replay_does_not_recapture`<br>`Wasla.Tests.Integration.Phase17GovernanceNotificationTests.E06_governance_keeps_existing_email_and_adds_only_eligible_own_inbox` |
| R06 | PASS | `Wasla.Tests.Integration.Phase17RecipientNotificationTests.R06_R07_R09_R11_family_membership_and_current_pairwise_authority_are_distinct` |
| R07 | PASS | `Wasla.Tests.Integration.Phase17RecipientNotificationTests.R06_R07_R09_R11_family_membership_and_current_pairwise_authority_are_distinct` |
| R08 | PASS | `Wasla.Tests.Integration.Phase17RecipientNotificationTests.R08_revoked_guardian_authority_prevents_dependent_reminder_delivery` |
| R09 | PASS | `Wasla.Tests.Integration.Phase17RecipientNotificationTests.R06_R07_R09_R11_family_membership_and_current_pairwise_authority_are_distinct` |
| R10 | PASS | `Wasla.Tests.Unit.Phase17NotificationTests.R10_R11_logical_audiences_keep_distinct_inboxes_and_missing_owner_can_only_be_suppressed`<br>`Wasla.Tests.Integration.Phase17RecipientNotificationTests.R06_R07_R09_R11_family_membership_and_current_pairwise_authority_are_distinct` |
| R11 | PASS | `Wasla.Tests.Unit.Phase17NotificationTests.R10_R11_logical_audiences_keep_distinct_inboxes_and_missing_owner_can_only_be_suppressed`<br>`Wasla.Tests.Integration.Phase17RecipientNotificationTests.R06_R07_R09_R11_family_membership_and_current_pairwise_authority_are_distinct` |
| R12 | PASS | `Wasla.Tests.Integration.Phase17RecipientNotificationTests.R01_R04_R12_trust_survives_otp_cleanup_but_does_not_follow_email_reassignment_or_account_closure` |
| R13 | PASS | `Wasla.Tests.Integration.Phase17NotificationTests.R03_R04_R13_E01_E05_no_trusted_email_still_commits_and_delivers_only_own_inboxes` |
| R14 | PASS | `Wasla.Tests.Integration.Phase17RecipientNotificationTests.R14_deactivated_reception_assignment_denies_operational_inbox_access`<br>`Wasla.Tests.Integration.DoctorOnboardingApiTests.OperationalPracticeApi_EnforcesOwnershipLifecycleSchedulingPricingAndReceptionScope` |
| R15 | PASS | `Wasla.Tests.Integration.Phase17NotificationTests.R03_R04_R13_E01_E05_no_trusted_email_still_commits_and_delivers_only_own_inboxes`<br>`Wasla.Tests.Integration.DoctorOnboardingApiTests.OperationalPracticeApi_EnforcesOwnershipLifecycleSchedulingPricingAndReceptionScope` |
| R16 | PASS | `Wasla.Tests.Integration.Phase17ClinicalNotificationTests.C09_R16_current_family_booking_authority_and_parent_optin_do_not_create_dependent_clinical_delivery` |
| E01 | PASS | `Wasla.Tests.Integration.Phase17LifecycleNotificationTests.R05_E01_O01_controlled_email_cutover_has_one_owner_and_replay_does_not_recapture`<br>`Wasla.Tests.Integration.Phase17NotificationTests.A08_A09_A11_A12_mutations_preserve_history_and_invalidate_previous_revisions` |
| E02 | PASS | `Wasla.Tests.Integration.Phase17LifecycleNotificationTests.E02_NoShow_and_expiration_capture_only_inapp_and_do_not_change_source_lifecycle` |
| E03 | PASS | `Wasla.Tests.Integration.Phase17LifecycleNotificationTests.A16_E03_successful_normal_and_force_checkin_capture_one_attendance_update` |
| E04 | PASS | `Wasla.Tests.Unit.Phase17NotificationTests.E04_E08_unapproved_triggers_cannot_be_captured`<br>`Wasla.Tests.Architecture.Phase17NotificationArchitectureTests.E04_E07_E08_C11_O14_contracts_have_no_arbitrary_send_or_clinical_payload_surface` |
| E05 | PASS | `Wasla.Tests.Integration.Phase17NotificationTests.R03_R04_R13_E01_E05_no_trusted_email_still_commits_and_delivers_only_own_inboxes` |
| E06 | PASS | `Wasla.Tests.Integration.Phase17GovernanceNotificationTests.E06_governance_keeps_existing_email_and_adds_only_eligible_own_inbox` |
| E07 | PASS | `Wasla.Tests.Architecture.Phase17NotificationArchitectureTests.E04_E07_E08_C11_O14_contracts_have_no_arbitrary_send_or_clinical_payload_surface`<br>`Wasla.Tests.Integration.DoctorOnboardingApiTests.OperationalPracticeApi_EnforcesOwnershipLifecycleSchedulingPricingAndReceptionScope` |
| E08 | PASS | `Wasla.Tests.Unit.Phase17NotificationTests.E04_E08_unapproved_triggers_cannot_be_captured` |
| E09 | PASS | `Wasla.Tests.Integration.Phase17NotificationTests.E09_E10_preferences_are_concurrent_independent_and_checked_at_dispatch` |
| E10 | PASS | `Wasla.Tests.Integration.Phase17NotificationTests.E09_E10_preferences_are_concurrent_independent_and_checked_at_dispatch` |
| E11 | PASS | `Wasla.Tests.Integration.TrustAccessApiTests.PasswordRecovery_HashesProofsAndConsumesChallengeOnce`<br>`Wasla.Tests.Unit.Phase17NotificationTests.E09_E10_E11_E12_preferences_are_independent_and_essential_categories_are_not_editable` |
| E12 | PASS | `Wasla.Tests.Integration.Phase17ClinicalNotificationTests.C03_C04_C05_C06_C07_C08_C09_C11_E12_E13_diagnostic_transitions_are_durable_generic_and_self_only` |
| E13 | PASS | `Wasla.Tests.Integration.Phase17ClinicalNotificationTests.C03_C04_C05_C06_C07_C08_C09_C11_E12_E13_diagnostic_transitions_are_durable_generic_and_self_only` |
| E14 | PASS | `Wasla.Tests.Unit.Phase17NotificationTests.E14_E15_E16_templates_fallback_version_and_encode_only_safe_booking_fields` |
| E15 | PASS | `Wasla.Tests.Unit.Phase17NotificationTests.E14_E15_E16_templates_fallback_version_and_encode_only_safe_booking_fields` |
| E16 | PASS | `Wasla.Tests.Unit.Phase17NotificationTests.E14_E15_E16_templates_fallback_version_and_encode_only_safe_booking_fields` |
| E17 | PASS | `Wasla.Tests.Unit.Phase17NotificationTests.Grace_window_is_inclusive_and_has_no_quiet_hours` |
| C01 | PASS | `Wasla.Tests.Integration.Phase17ClinicalNotificationTests.C01_C02_C10_prescription_draft_finalize_hidden_navigation_and_source_reauthorization` |
| C02 | PASS | `Wasla.Tests.Integration.Phase17ClinicalNotificationTests.C01_C02_C10_prescription_draft_finalize_hidden_navigation_and_source_reauthorization` |
| C03 | PASS | `Wasla.Tests.Integration.Phase17ClinicalNotificationTests.C03_C04_C05_C06_C07_C08_C09_C11_E12_E13_diagnostic_transitions_are_durable_generic_and_self_only` |
| C04 | PASS | `Wasla.Tests.Integration.Phase17ClinicalNotificationTests.C03_C04_C05_C06_C07_C08_C09_C11_E12_E13_diagnostic_transitions_are_durable_generic_and_self_only` |
| C05 | PASS | `Wasla.Tests.Integration.Phase17ClinicalNotificationTests.C03_C04_C05_C06_C07_C08_C09_C11_E12_E13_diagnostic_transitions_are_durable_generic_and_self_only` |
| C06 | PASS | `Wasla.Tests.Integration.Phase17ClinicalNotificationTests.C03_C04_C05_C06_C07_C08_C09_C11_E12_E13_diagnostic_transitions_are_durable_generic_and_self_only` |
| C07 | PASS | `Wasla.Tests.Integration.Phase17ClinicalNotificationTests.C03_C04_C05_C06_C07_C08_C09_C11_E12_E13_diagnostic_transitions_are_durable_generic_and_self_only` |
| C08 | PASS | `Wasla.Tests.Integration.Phase17ClinicalNotificationTests.C01_C02_C10_prescription_draft_finalize_hidden_navigation_and_source_reauthorization`<br>`Wasla.Tests.Integration.Phase17ClinicalNotificationTests.C03_C04_C05_C06_C07_C08_C09_C11_E12_E13_diagnostic_transitions_are_durable_generic_and_self_only` |
| C09 | PASS | `Wasla.Tests.Integration.Phase17ClinicalNotificationTests.C09_R16_current_family_booking_authority_and_parent_optin_do_not_create_dependent_clinical_delivery` |
| C10 | PASS | `Wasla.Tests.Integration.Phase17ClinicalNotificationTests.C01_C02_C10_prescription_draft_finalize_hidden_navigation_and_source_reauthorization` |
| C11 | PASS | `Wasla.Tests.Architecture.Phase17NotificationArchitectureTests.E04_E07_E08_C11_O14_contracts_have_no_arbitrary_send_or_clinical_payload_surface`<br>`Wasla.Tests.Integration.Phase17ClinicalNotificationTests.C03_C04_C05_C06_C07_C08_C09_C11_E12_E13_diagnostic_transitions_are_durable_generic_and_self_only` |
| C12 | PASS | `Wasla.Tests.Integration.Phase17NotificationTests.A19_O09_C12_api_permissions_admin_metadata_and_denial_audit_are_enforced`<br>`Wasla.Tests.Integration.Phase17NotificationSqlServerTests.O10_admin_retry_is_atomic_audited_and_rejects_already_claimed_work`<br>`Wasla.Tests.Architecture.Phase17NotificationArchitectureTests.E04_E07_E08_C11_O14_contracts_have_no_arbitrary_send_or_clinical_payload_surface` |
| O01 | PASS | `Wasla.Tests.Integration.Phase17LifecycleNotificationTests.R05_E01_O01_controlled_email_cutover_has_one_owner_and_replay_does_not_recapture`<br>`Wasla.Tests.Unit.Phase17NotificationTests.O01_O11_terminal_intents_and_idempotent_reads_preserve_logical_identity` |
| O02 | PASS | `Wasla.Tests.Integration.Phase17NotificationSqlServerTests.O02_two_real_sql_workers_create_one_logical_delivery_per_channel` |
| O03 | PASS | `Wasla.Tests.Integration.Phase17NotificationSqlServerTests.O03_source_commit_serializes_with_dispatch_and_invalidates_old_revision`<br>`Wasla.Tests.Integration.Phase17SmtpBoundaryTests.O03_O04_smtp_admission_serializes_source_commit_without_holding_sql_transaction`<br>`Wasla.Tests.Integration.Phase17NotificationTests.Send_time_guard_suppresses_expired_or_cancelled_reminders_before_smtp` |
| O04 | PASS | `Wasla.Tests.Integration.Phase17ReliabilityNotificationTests.O04_provider_acceptance_before_an_unobserved_ack_can_duplicate_physical_delivery_but_not_logical_identity`<br>`Wasla.Tests.Integration.Phase17SmtpBoundaryTests.O03_O04_smtp_admission_serializes_source_commit_without_holding_sql_transaction` |
| O05 | PASS | `Wasla.Tests.Integration.Phase17NotificationSqlServerTests.O05_O08_smtp_failure_retries_independently_and_expired_reminder_is_suppressed`<br>`Wasla.Tests.Integration.EmailOutboxTests.TransientFailure_SchedulesRetry_WithoutDeletingMessage` |
| O06 | PASS | `Wasla.Tests.Integration.Phase17ReliabilityNotificationTests.O06_O08_permanent_authentication_failure_is_safe_and_stale_admin_retry_is_denied` |
| O07 | PASS | `Wasla.Tests.Integration.Phase17NotificationSqlServerTests.O07_expired_claim_is_recovered_once_and_unique_delivery_is_enforced_by_sql`<br>`Wasla.Tests.Integration.EmailOutboxTests.ProcessingLease_PreventsEarlyClaimAndAllowsRecoveryAfterExpiry` |
| O08 | PASS | `Wasla.Tests.Integration.Phase17ReliabilityNotificationTests.O06_O08_permanent_authentication_failure_is_safe_and_stale_admin_retry_is_denied`<br>`Wasla.Tests.Integration.Phase17NotificationSqlServerTests.O05_O08_smtp_failure_retries_independently_and_expired_reminder_is_suppressed`<br>`Wasla.Tests.Integration.Phase17NotificationTests.Send_time_guard_suppresses_expired_or_cancelled_reminders_before_smtp`<br>`Wasla.Tests.Integration.Phase17NotificationSqlServerTests.O10_admin_retry_is_atomic_audited_and_rejects_already_claimed_work` |
| O09 | PASS | `Wasla.Tests.Integration.Phase17NotificationTests.A19_O09_C12_api_permissions_admin_metadata_and_denial_audit_are_enforced`<br>`Wasla.Tests.Integration.Phase17NotificationSqlServerTests.O10_admin_retry_is_atomic_audited_and_rejects_already_claimed_work` |
| O10 | PASS | `Wasla.Tests.Integration.Phase17NotificationSqlServerTests.O10_admin_retry_is_atomic_audited_and_rejects_already_claimed_work` |
| O11 | PASS | `Wasla.Tests.Integration.Phase17NotificationTests.O11_O12_inbox_pagination_counts_and_idempotent_read_are_owner_scoped` |
| O12 | PASS | `Wasla.Tests.Integration.Phase17ReliabilityNotificationTests.O12_large_inbox_has_bounded_stable_pages_counts_and_own_bulk_marking` |
| O13 | PASS | `Wasla.Tests.Integration.Phase18PartTwoSqlTests.Linked_group_rolls_back_each_database_step_then_replays_and_refunds_without_unarchiving`<br>`Wasla.Tests.Integration.Phase18PartTwoSqlTests.Closed_noshow_keeps_status_releases_patient_lease_and_missing_payment_is_quarantined` |
| O14 | PASS | `Wasla.Tests.Architecture.Phase17NotificationArchitectureTests.E04_E07_E08_C11_O14_contracts_have_no_arbitrary_send_or_clinical_payload_surface`<br>`Wasla.Tests.Unit.Phase17NotificationTests.E14_E15_E16_templates_fallback_version_and_encode_only_safe_booking_fields` |
| O15 | PASS | `Wasla.Tests.Integration.Phase17NotificationMigrationTests.O15_model_snapshot_matches_and_migrations_are_additive_without_backfill`<br>`Wasla.Tests.Integration.Phase17NotificationMigrationTests.O15_populated_legacy_outbox_survives_native_sql_upgrade_without_resend_or_backfill` |
| O16 | PASS | `Wasla.Tests.Architecture.Phase17NotificationArchitectureTests.E04_E07_E08_C11_O14_contracts_have_no_arbitrary_send_or_clinical_payload_surface` |
| O17 | PASS | `Wasla.Tests.Architecture.Phase17NotificationArchitectureTests.E04_E07_E08_C11_O14_contracts_have_no_arbitrary_send_or_clinical_payload_surface` |
| O18 | PASS | `Wasla.Tests.Integration.Phase17LifecycleNotificationTests.A17_O18_walkin_and_feature_disable_never_create_historical_reminders`<br>`Wasla.Tests.Integration.Phase17NotificationSqlServerTests.O07_expired_claim_is_recovered_once_and_unique_delivery_is_enforced_by_sql` |

Full data-row names, durations and originating run files are in TEST-EVIDENCE.json.

Shared-address coverage preserves recipient identity and excludes ineligible family audiences. Existing unique account-email rules are retained; this module does not create shared accounts or fabricate verified contacts.

The native SQL acceptance runs use real SQL Server and isolated test databases. Physical SMTP uncertainty uses a deterministic fake provider, including simulated acceptance with a lost response. No production SMTP delivery is asserted.

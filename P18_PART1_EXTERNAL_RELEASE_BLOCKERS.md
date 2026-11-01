# Phase 18 Part One external release blockers

- **P0 — exposed configuration credentials:** Existing application settings are preserved exactly by owner instruction. Previously public sensitive-looking credentials require separate authorized rotation/revocation, repository-history review, and secure deployment. Values are intentionally omitted. Production release is blocked until remediation is independently verified.
- Existing Phase 16 clinical sharing requires privacy/consent/legal approval. Disabled cross-doctor and guardian gates remain disabled; dashboard navigation must express unavailable access.
- Clinical/operational retention and deletion durations remain unapproved. No purge or archive implementation belongs to this delivery.
- Production PDF hosting must verify automatic browser provisioning/launch under the real IIS Application Pool identity, default-cache write/execute access, download-source HTTPS access and Arabic fonts. CustomerSurvey-compatible flags disable Chromium sandbox/web security. Follow docs/phase18/export-runtime-iis-smoke.md; local verification does not approve a production runtime.

Technical implementation status and production release approval are separate. This document does not declare production readiness.

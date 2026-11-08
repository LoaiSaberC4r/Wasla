# Family Production deployment

All configuration, including credentials and certificate password, stays in
appsettings. Do not use environment-variable, User Secrets or vault overrides.
The checked-in Production target enables every in-scope feature. It intentionally
requires the real host paths before startup; do not substitute guessed paths.

## Scanner

For a host supporting Docker, run
`docker compose -f deploy/family-production/clamav.compose.yml -p wasla-family-scanner up -d`.
The image and digest are pinned; freshclam updates its durable signature volume.
Publish only 127.0.0.1:3310. The appsettings loopback address, port, 30-second
timeout and 10 MB stream limit match this deployment. Monitor container health,
freshclam/signature age and `/health`. Upgrade the pinned image through a tested
deployment. Never expose unauthenticated ClamD TCP to a public network.

Run ProductionDependencyTests for clean, standard in-memory EICAR, unavailable,
timeout and encrypted persistent-key tests. HistoricalValidationLifecycleTests
exercise ClamD verdict handling and scanner unavailability through SQL-backed
upload/replay/private-read paths; the standalone EICAR test uses the actual daemon.
ClamAV's [official Docker guide](https://docs.clamav.net/manual/Installing/Docker.html)
describes the daemon and signature updater.

MonsterASP's [supported technologies](https://help.monsterasp.net/getting-started/first-steps/supported-technologies)
exclude Docker and restrict launching executable processes from websites. Its
[staff background-worker guidance](https://forum.monsterasp.net/d/104-aspnet-core-background-services-support)
requires Premium AlwaysRunning for continuous execution. A 2 GB SQL database
package does not establish the website plan or daemon support. If the API also
runs there, obtain written provider confirmation of an approved local authenticated
tunnel/sidecar to an isolated scanner host. The current client accepts loopback
only. If a local tunnel cannot be provisioned on that shared host, deploy the API
on a VM/container-capable host alongside the private scanner, while retaining the
MonsterASP SQL database. Do not weaken the loopback check or publish ClamD.

## Keys and private storage

Confirm the host's persistent writable private-media root, absolute key-ring
directory and private PFX path. Set MedicalRecord:DataProtectionKeyRingPath,
DataProtectionCertificatePath and DataProtectionCertificatePassword in Production
appsettings. For IIS, use Prepare-PrivateStorage.ps1 with the real app-pool
identity. Restrict PFX read access to that identity and administrators; keep it
outside webroot. Use a dedicated encryption certificate, preserve its private key,
and back it up separately with its password. All nodes must use the same mounted
key ring, encryption certificate, application name Wasla.Phase16 and stable
OpaqueEntryKey. Never regenerate or remove them during publish. Restore all three
with the matching database/media backup. Keep prior decryption certificates and
key files across rotations until all protected references have expired.

See [Microsoft's Data Protection guidance](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0).
Production startup verifies policy, flags, protected-key roundtrip and scanner
reachability. `/health` reports scanner/configuration failure without secret values.

## SMTP and workers

Preserve existing SMTP configuration. The probe reads appsettings and connects
with mandatory STARTTLS, then authenticates. With `--send`, it queues the single
approved synthetic message in a newly created local SQL Express database and uses
the real EmailOutboxProcessor and MailKit sender. It validates duplicate queueing,
Sent state and a second empty processing batch. A durable local pre-send marker
prevents another send even after an uncertain acknowledgement. Never delete that
marker to retry without new owner authorization. Run:

`dotnet run --project tools/Wasla.ProductionProbe -- E:/Wasla --send`

The probe never uses Production SQL and never prints credentials. Authentication
failure sends nothing. SMTP acceptance is distinct from inbox delivery; recipient
confirmation or provider delivery logs are needed for the latter. Production must
keep the outbox, notification and reconciliation workers continuously running;
monitor failed deliveries, attempt counts/leases, private-media discrepancy
reports and scanner health. Clinical recipient eligibility remains self-only.

## Deployment and rollback

1. Confirm API hostname, plan, publish profile, persistent volumes, certificate,
   instance count/shared storage and continuous worker support. Do not deploy
   with unset required host paths.
2. Take a consistent SQL backup with CHECKSUM plus private-media/key/certificate
   backups. Verify restore into a disposable database. Retain existing IDs.
3. Rehearse the generated idempotent docs/family-self-service/MIGRATION.sql on a
   clean database, representative populated copy and restored backup. Apply it
   twice and verify migration history, foreign keys and retained identities.
4. Schedule a controlled deployment window; apply the reviewed migration script
   and additive security seeding with operator credentials. Startup migration
   and seeding remain false. No task command targets db73076.
5. Publish without overwriting persistent media/keys. Start the scanner and API;
   verify `/health`, a synthetic authorized/denied dependent journey, outbox worker
   progress and per-module reconciliation metrics. Record the deployed commit.
6. If verification fails, restore the previous application package or the matched
   pre-release SQL/media/key backups under the incident procedure. New migrations
   retain audit/evidence and deliberately prohibit destructive Down operations.
   Do not delete orphan candidates automatically or enable unrestricted access.

Reconciliation processes six real modules with SQL-backed cursors. Findings are
private PrivateMediaReconciliationRuns rows keyed by ReportId; investigate their
FindingsJson using authorized operator access. Missing files and orphan candidates
are reported, never deleted automatically. Back up these checkpoints/reports with
the database; failed batches retry without advancing their cursor.

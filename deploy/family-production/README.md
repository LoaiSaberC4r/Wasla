# Family Production deployment

All configuration, including credentials and certificate password, stays in
appsettings. Do not use environment-variable, User Secrets or vault overrides.
The checked-in Production target enables every in-scope feature. Its private
storage paths are portable App_Data paths resolved against the application's
ContentRootPath. The actual storage and encryption certificate must be provisioned
before startup; relative configuration alone does not establish host readiness.

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

The approved [storage decision v3](../../docs/family-self-service/DECISION-2026-11-08-v3.md)
uses this layout relative to IHostEnvironment.ContentRootPath:

```text
App_Data/Wasla/
  Media/
    Doctors/
    Patients/
    MedicalRecords/
  DataProtection/KeyRing/
  Certificates/Wasla-DataProtection.pfx
```

Production appsettings retains MediaStorage:RootPath=App_Data/Wasla/Media,
MedicalRecord:DataProtectionKeyRingPath=App_Data/Wasla/DataProtection/KeyRing and
MedicalRecord:DataProtectionCertificatePath=App_Data/Wasla/Certificates/Wasla-DataProtection.pfx.
Do not replace these with provider-specific absolute paths. Existing media module
subdirectories and keys are unchanged; copy their complete contents during a move.

Provision the directories before starting the application. Give the application
identity read/write access to Media and KeyRing, and read access to Certificates
and the PFX; restrict other identities. For IIS, the existing
Prepare-PrivateStorage.ps1 requires the actual absolute ContentRootPath and
WebRootPath reported by the application, the resolved physical Media and KeyRing
directories and the real app-pool identity. Do not infer WebRootPath from a
directory name. For example, after supplying both actual host roots:

```powershell
./deploy/family-production/Prepare-PrivateStorage.ps1 `
  -ContentRootPath $contentRoot -WebRootPath $webRoot `
  -PrivateMediaPath (Join-Path $contentRoot 'App_Data/Wasla/Media') `
  -KeyRingPath (Join-Path $contentRoot 'App_Data/Wasla/DataProtection/KeyRing') `
  -ApplicationIdentity $applicationIdentity
```

Use `-WhatIf` to validate the paths without creating directories or changing ACLs.
Run `pwsh -NoProfile -File tools/verify-private-storage-paths.ps1` on Windows for
the focused containment checks, including an ancestor named wwwroot.
Both targets must remain inside ContentRootPath/App_Data/Wasla and outside the
actual WebRootPath. An ancestor named wwwroot alone does not make a target public.
Provision the Certificates directory and its read-only application permission
separately. Keep all of App_Data outside the actual WebRootPath. Neither the web
server nor an application Static Files mapping may expose
it; retain only the authorized private-media retrieval flow and verify unauthenticated
direct file requests cannot retrieve any private file on the actual host.

Upload the real dedicated encryption PFX, including its private key, to the exact
relative certificate path above. Set its actual DataProtectionCertificatePassword
in Production appsettings if password-protected. No production certificate or
password is supplied or generated by the application. Missing/invalid private-key
material prevents startup; ephemeral keys are not an acceptable fallback.

App_Data is excluded from Git and application publish artifacts. Configure the
MonsterASP deployment process to preserve App_Data, including when deleting old
application files. For another provider, provision its persistent directory or
mounted-volume equivalent at the same application-relative paths. Publish
exclusion alone does not prevent a deployment tool from deleting remote files.
Never regenerate, replace or remove storage contents during a normal publish.

All instances must map their relative KeyRing paths to the same shared persistent
key ring and use compatible encryption/private-key material, application name
Wasla.Phase16, existing Data Protection purposes and the stable OpaqueEntryKey.
Preserve prior decryption certificates and key files across rotation until all
protected references have expired. Back up the complete App_Data folder and
certificate recovery material securely and consistently with the database; retain
the corresponding appsettings secrets. Test restoring database, media, full key
ring and certificate together. A provider migration requires copying App_Data
contents and the corresponding database backup; relative paths transfer no files.

See [Microsoft's Data Protection guidance](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0).
Production startup verifies policy, flags, provisioned writable Media and KeyRing directories,
certificate private key, protected-key roundtrip, scanner reachability and SMTP
authentication. Startup and `/health` create, flush and immediately remove unique
non-sensitive temporary files in Media and KeyRing. Missing directories are not
created by these probes. `/health` also requires a readable certificate file and
reports storage/scanner/configuration failure without physical paths or secret values.

CI runs `bash tools/verify-app-data-publish-exclusion.sh` after the Release build.
This publishes into an isolated temporary directory, rejects any App_Data or
sentinel in the output, and cleans its own sentinel and temporary output on success
or failure. It never removes existing App_Data contents.

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
   before provisioning the configured App_Data directories and real PFX.
2. Take a consistent SQL backup with CHECKSUM plus private-media/key/certificate
   backups. Verify restore into a disposable database. Retain existing IDs.
3. Rehearse the generated idempotent docs/family-self-service/MIGRATION.sql on a
   clean database, representative populated copy and restored backup. Apply it
   twice and verify migration history, foreign keys and retained identities.
4. Schedule a controlled deployment window; apply the reviewed migration script
   and additive security seeding with operator credentials. Startup migration
   and seeding remain false. No task command targets db73076.
5. Publish while preserving the complete App_Data folder. Start the scanner and API;
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

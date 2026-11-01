# IIS PDF and Excel deployment smoke procedure

This verifies the approved 2026-10-29 CustomerSurvey-compatible runtime. Run deployment steps through the existing release process. Use synthetic authorized clinical/report records, a restricted output folder and the **real target Application Pool identity**. Developer-machine browser tests do not establish production IIS readiness.

## Publish and inspect

```powershell
dotnet restore Wasla.sln
dotnet build Wasla.sln -c Release --no-restore
dotnet publish src/Wasla/Wasla.Api/Wasla.Api.csproj -c Release --no-build --no-restore -o E:\deploy\Wasla-publish
Get-ChildItem E:\deploy\Wasla-publish\Views\Templates -Recurse -Filter *.cshtml
Test-Path E:\deploy\Wasla-publish\Views\Templates\Report.cshtml
```

Compare the source `Wasla.Api/Views/Templates/**/*.cshtml` inventory with the same relative paths in build/publish output; currently only `Report.cshtml` is required. Check `Wasla.Api.deps.json` contains compilation libraries and `refs/` exists. Keep `PreserveCompilationContext`, `CopyLocalLockFileAssemblies` and all template CopyToOutputDirectory/CopyToPublishDirectory settings enabled.

Parse generated `web.config` as XML and confirm there is exactly one `aspNetCore` handler using `AspNetCoreModuleV2`, plus one `aspNetCore` element with the existing hosting model. Framework-dependent deployment uses `dotnet` and `.\Wasla.Api.dll`. Do not add another handler. Install the .NET 10 ASP.NET Core Hosting Bundle appropriate to the deployment architecture and use the existing IIS binding/TLS/reverse-proxy configuration.

## Identity, cache and network

1. Identify the actual site, Application Pool name, identity, bitness, recycle/overlap and web-garden settings in IIS Manager. Deploy the inspected output using the established process; preserve configuration/secret overrides, private media, Data Protection keys and existing worker/database switches. This task adds no migration.
2. Remove obsolete `Phase18__ChromiumExecutablePath` and `WASLA_CHROMIUM_PATH` requirements. Also ensure `PUPPETEER_EXECUTABLE_PATH`/revision overrides do not redirect the smoke test to a separately installed browser. No administrator-installed Chrome is needed.
3. PuppeteerSharp 25.10.0's default `BrowserFetcher` cache is resolved alongside `PuppeteerSharp.dll` (normally the published application directory). The server log event 1810 reports the resolved cache directory. Its `Chrome/` and `ChromeHeadlessShell/` subdirectories contain the downloaded pinned browser; download archives are created at the cache root. Do not assume a developer user-profile cache. Grant **only the target worker identity** the required create/write access for root archives and narrowly scoped Modify rights on browser cache subdirectories, plus read/execute rights on extracted executables. Avoid blanket Modify rights on the application tree and protect templates/assemblies from writes. This default application-directory cache adds a deployment permission consideration beyond merely copying templates. The worker also needs writable OS temp/profile space, disk capacity for archives/extraction and permission to start child processes. Follow local ACL/antivirus/AppLocker policy; do not grant Everyone/Users broad access. Test a genuinely empty cache in an isolated rehearsal site or with the pool stopped and cache safely backed up, never delete a live shared installation.
4. Permit outbound HTTPS from the worker process to `storage.googleapis.com/chrome-for-testing-public`; PuppeteerSharp may fall back to `googlechromelabs.github.io/chrome-for-testing` metadata and its published download URL. Verify DNS, TLS trust/proxy/service-account network policy and Windows executable extraction/antivirus scanning under the actual identity. An interactive administrator's successful download is insufficient. BrowserFetcher installs both Chrome and Chrome Headless Shell with this package version. Preserve the cache across deployments/recycles when compatible with the deployed package version; a clean deployment can reprovision automatically.
5. Confirm OS/browser architecture and runtime dependencies, process-creation policy, and Arabic-capable fonts (Tahoma/Segoe UI/Arial) on the server. Package installation does not install Arabic fonts. Check actual Arabic shaping and mixed-direction values in the exported pages.
6. The guard coordinates a single worker process. Separate IIS workers/overlapping recycles are separate processes: avoid concurrent first provisioning into the same empty cache by rehearsing with one worker and serializing initial warm-up/recycle. Once populated, the on-disk browser cache is reused after restart. This is the same process-wide scope as CustomerSurvey.

CustomerSurvey parity uses `--disable-web-security`, `--allow-file-access`, `--allow-file-access-from-files`, `--no-sandbox`, `--disable-setuid-sandbox`. These remove Chromium security boundaries. Keep the worker minimally privileged; render only encoded, trusted server HTML and the fixed deployed Razor template. Preserve CSP, disabled page scripts, offline rendering, resource interception, private-media checks and clinical/report authorization. Never expose a client HTML/template-name endpoint.

## Authenticated production rehearsal

1. Start the application under the actual pool identity. Confirm the application starts without downloading a browser. Check ordinary application/DB health according to existing operations policy. With a permitted SuperAdmin token, GET `/api/v1/admin/operations/exports/health`: expect `NotMeasured`, null probe fields, `browserProvisioning: AutomaticDownload`, and legacy `executableConfigured: false` before any report PDF attempt. Reading this endpoint does not trigger a probe/download.
2. Use a Doctor with the existing prescription/report export permissions and synthetic finalized prescription/practice data. Put a short-lived bearer token in the process-only `WASLA_SMOKE_DOCTOR_TOKEN` environment variable without logging it. Run:

   ```powershell
   ./scripts/phase18/Invoke-IisExportSmoke.ps1 -BaseUri https://your-existing-host/ `
       -PrescriptionId <issued-prescription-guid> -Version 1 -PracticeId <authorized-practice-guid> `
       -OutputDirectory E:\restricted-qa\wasla-initial
   ```

   The script calls existing authenticated prescription-PDF, reporting-PDF and XLSX routes; checks MIME, filename/extension, no-store, PDF signatures and the seven XLSX worksheet names. It never installs a browser itself. The first PDF must cause browser provisioning inside the worker. If a slow first download exceeds the normal 45-second request deadline, expect controlled 503 `RenderTimeout`, let the logged shared download finish, then retry. Wasla keeps the existing validated 5–120 second deadline; Puppeteer's internal launch/content timeouts remain 1,000,000 ms. Download failure is distinct from a request that simply stopped waiting.
3. Confirm server log 1810/1811 and browser files owned/accessed by the pool identity. Reporting health becomes `Ready` with successful timestamp only after the report's `%PDF` signature and byte limit pass; downloaded-browser metadata alone is not rendering readiness.
4. Open both PDFs in a reader. Check actual prescription version/status, names and medication details; test Finalized, Superseded and Voided history using legitimate issued versions. Verify Arabic shaping/RTL, English labels, A4 size, no clipping/overlap, report metadata/filter/quality/calculations, repeating table headers and footer `pageNumber / totalPages` on first/middle/last pages. Request `language=en&landscape=true` on the existing report route and verify landscape A4. The synthetic browser test also exercises these layouts from a published template.
5. Reopen XLSX in Microsoft Excel/LibreOffice or a small .NET reader using `new XLWorkbook(stream)`. Check all seven sheets, Arabic RTL/English labels, numeric EGP/percentage/date cells, details, charts, comparisons, literal formula-like references and empty/large reports. A ZIP signature alone does not establish workbook correctness; the automated unit/browser suites also reopen with ClosedXML.
6. Run the script again into a second restricted directory. Confirm no new download on subsequent requests and all three formats continue to work. Respect the existing export rate limit of 12 requests per actor per 15 minutes. Do not disable authorization/rate limits for the test.
7. Recycle the pool using existing IIS operations, then repeat the script. Verify application startup remains independent of browser provisioning and first post-recycle exports discover/reuse the browser cache without an executable setting. Test cache recovery separately in an isolated rehearsal; preserve the original cache and other workers.
8. In an isolated rehearsal site with an empty cache, deny the required outbound HTTPS (or remove cache write permission) for that test identity and request a PDF. Expect controlled 503 and stage/type-only log diagnostics (`BrowserDownloadFailed`, or `RenderTimeout` if the bounded wait expires first), no HTML/medical data/raw exception in the response, and no unhandled startup exception. XLSX must still succeed. Restore the permission/network rule, allow any outstanding non-cancellable fetch to finish/fail, and retry: failures must be retryable. Separately test a blocked browser executable policy: expect controlled `BrowserLaunchFailed` and no leaked bytes.
9. Verify unauthorized/other-Doctor/revoked-scope requests still deny exports and existing export audits contain metadata, not file/medical contents. Clear process tokens and retain synthetic evidence according to the QA retention policy.

## Local automated reproduction

```powershell
$env:WASLA_RUN_BROWSER_TESTS='1'
$env:WASLA_PUBLISHED_API_DIRECTORY='E:\deploy\Wasla-publish'
$env:WASLA_EXPORT_QA_DIRECTORY='E:\restricted-qa\synthetic'
Remove-Item Env:WASLA_CHROMIUM_PATH,Env:Phase18__ChromiumExecutablePath,Env:PUPPETEER_EXECUTABLE_PATH -ErrorAction SilentlyContinue
dotnet test tests/Wasla.Tests.Integration -c Release --filter FullyQualifiedName~ExportBrowserSmokeTests
```

This verifies published physical-template compilation, production automatic provisioning and real output from a developer test process. It does **not** replace steps under the real IIS identity. Record host/pool/identity/architecture, release hash, templates, cache path/ACL, network policy, fonts, health before/after, output hashes and actual results. Current executed evidence and outstanding blockers are in [the implementation report](customer-survey-export-runtime-report.md).

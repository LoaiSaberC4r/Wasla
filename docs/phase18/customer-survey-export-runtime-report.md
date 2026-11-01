# CustomerSurvey-compatible export runtime implementation

Date: 2026-10-29. Branch: `codex/customer-survey-export-runtime`.

## Root causes and implementation

Prescription and report renderers independently rejected an absent/invalid fixed browser executable before rendering, producing `ChromiumNotConfigured`. Reports compiled an embedded Razor template rather than a published physical file. The API lacked CustomerSurvey's compilation-context/assembly-copy properties, and a hosted PDF probe made startup perform browser work. The original report CSS also forced portrait A4 despite a landscape request; actual PDF geometry inspection exposed that issue during this change.

The reference [CustomerSurvey PdfService](https://github.com/NileTronixRepositories/CustomerSurvey/blob/master/CustomerSurvey.infrastructure/Reports/PdfService.cs), [RazorViewRenderer](https://github.com/NileTronixRepositories/CustomerSurvey/blob/master/CustomerSurvey.infrastructure/Reports/RazorViewRenderer.cs), [API project](https://github.com/NileTronixRepositories/CustomerSurvey/blob/master/CustomerSurvey.Api/CustomerSurvey.Api.csproj), and [BranchTemplatesPdfReport](https://github.com/NileTronixRepositories/CustomerSurvey/blob/master/CustomerSurvey.Api/Views/Templates/BranchTemplatesPdfReport.cshtml) were inspected directly. The PDF reference blob was `c07e8cbdba9f861c7fe65ba900e4125c13de9d35`; Razor renderer `c6027952e3c81555398da07031ab0837f0e39ebf`; API project `0a0a34e7d34f6bab9929931f7364a04c32496d41`.

Repository-wide inventory found two PDF entry points (`PrescriptionPdfRenderer` and `ReportDocumentRenderer`) and one workbook generator (`ReportDocumentRenderer.RenderExcel`), used by the existing role-specific report endpoints. Both PDF entry points now use one singleton `ChromiumPdfRenderer`, one process-wide download coordinator and the production `BrowserFetcher`/PuppeteerSharp runtime. No PDFsharp/DinkToPdf or other PDF engine was added. Central versions remain PuppeteerSharp 25.10.0, RazorLight 2.3.1, ClosedXML 0.105.1, targeting .NET 10.

## Final flow compared with CustomerSurvey

1. Generate encoded, trusted prescription HTML or compile the fixed `Report.cshtml` key with the existing `ReportPresentation` model.
2. Double-check download completion under a semaphore, then share one pending `new BrowserFetcher().DownloadAsync()` task across first requests. Reuse successful provisioning; retry failed provisioning. The coordinator is static across DI containers within the worker. BrowserFetcher rediscovers its disk cache after restart.
3. Launch headless with `Timeout=1_000_000` and exactly `--disable-web-security`, `--allow-file-access`, `--allow-file-access-from-files`, `--no-sandbox`, `--disable-setuid-sandbox`. No executable path is supplied.
4. Create a page, apply Wasla's existing disabled page scripts/offline mode/resource interception, set HTML with `Load` and `Networkidle0` and `Timeout=1_000_000`, await `document.fonts.ready`, emulate print and call `PdfDataAsync`.
5. Preserve A4/margins/background/header/footer options, report page counters, bilingual/RTL layout and prescription version/status/watermarks. CSS now expresses the requested portrait/landscape orientation.
6. Validate the PDF signature. Prescriptions retain the 15 MiB rejection in their adapter; reports retain the existing export-service byte-limit check and its 422 `Report.ExportTooLarge`/`Rejected` audit with actual byte count. Reports do not mark an oversized document as ready.
7. Dispose fonts/page/browser asynchronously. If cancellation occurs while a resource is still being acquired, observe its completion and dispose it; keep its concurrency slot occupied until cleanup completes. Preserve caller cancellation and controlled timeout/failure results. Diagnostics identify stage/type and infrastructure remedies without HTML, medical contents or raw exception messages.

Wasla retains the shared concurrency limit (default 2, validated 1–4) and outer deadline (default 45 seconds, validated 5–120). Unlike a cancellable operation, BrowserFetcher's API cannot stop an in-progress download: a cancelled/timed-out request stops waiting while the shared task finishes. A first download can exceed a request deadline and require a retry. The downloader is process-wide, not a cross-process cache lock; overlapping IIS workers must avoid concurrent first provisioning into an empty shared cache.

The flags deliberately disable Chromium security protections. The implementation retains only encoded server-generated HTML, a fixed allow-listed template, CSP, disabled page JavaScript, offline rendering and data/about-only interception. No client HTML/template route was added. Clinical/report authorization, scope rechecks, private-media policy, immutable audits, lifecycle rules, filenames, MIME types and limits remain in their existing application/service paths. No migration or appsettings modification was introduced.

## IIS and readiness changes

The API enables `PreserveCompilationContext` and `CopyLocalLockFileAssemblies`; physical templates are copied to build/publish output with `PreserveNewest`. The old embedded declaration/source is removed. RazorLight uses `IWebHostEnvironment.ContentRootPath/Views/Templates` and memory caching, with compilation references preserved. Lazy engine construction keeps missing templates isolated from startup and Excel. The generated web.config is not customized.

The startup probe service/registration is removed. Explicit server-side `ProbeAsync` remains, and ordinary report PDFs update measured health. The existing health fields/permission are preserved; `ExecutableConfigured` is a legacy false field, with additive `BrowserProvisioning=AutomaticDownload` and `BrowserDownloaded`. Download completion alone never reports successful rendering. `NotMeasured`/null probe values precede any report attempt.

The IIS worker needs appropriate default-cache archive/subdirectory write permissions, executable/temp access, outbound HTTPS and Arabic-capable fonts. PuppeteerSharp's normal cache is alongside its assembly, normally the deployment directory, rather than an assumed administrator user-profile cache. See the [complete IIS procedure](export-runtime-iis-smoke.md) for safe permission scoping, Hosting Bundle/web.config checks, authenticated exports, reuse/recycle, and actual failure/recovery rehearsal.

## Executed verification

Raw logs/TRX and synthetic PDF/XLSX/PNG evidence are under ignored `_local-data/export-runtime/`; historical Phase 18/Doctor Patients counters in older reports are not current-run results.

| Check actually executed | Result |
|---|---|
| Final Release solution rebuild | Passed, 0 warnings / 0 errors; `final-build.log`. |
| Release API publish | Passed; `Views/Templates/Report.cshtml`, 308 compilation-reference files and expected dependency metadata present. |
| Final full unit suite | **348 passed, 0 failed, 0 skipped**; `final-unit.trx`, 14 seconds. Includes PDF/concurrency/cancellation/lifecycle/size/health plus bilingual/25,000-row/empty ClosedXML regressions. |
| Final architecture suite | **26 passed, 0 failed, 0 skipped**; `final-architecture.trx`. |
| Final affected API/SQL/contract suite, real browser enabled | **10 passed, 0 failed, 0 skipped**; `final-export-api.trx`, approximately 1m46s. Includes issued historical PDFs over SQLite/SQL Server, authorization/audit denial, XLSX/controlled download failure, both format-size rejection audits, report archive parity with real Arabic/English PDF exports and API metadata. |
| Final cold-cache actual-browser smoke | **1 passed**; `final-browser.trx`, approximately 2m12s. Used automatic download, published physical template and no executable overrides, with real PDF A4 geometry assertions. |
| Same smoke in a new process, existing browser cache | **1 passed**; `browser-restart-cache.trx`, approximately 25s. Repeated report/prescription PDFs and XLSX without another cold download. This is local process restart, not IIS recycle. |
| Published API dependency-context probe | Passed. Temporary helper executed with published `Wasla.Api.deps.json` and `Wasla.Api.runtimeconfig.json` from the publish directory: physical Razor compiled to 2,761 HTML characters, XLSX reopened with 7 sheets, health remained NotMeasured and BrowserDownloaded false. Helper DLL removed afterward; source/log retained locally. |
| Generated web.config XML | Passed: one `aspNetCore` handler, `AspNetCoreModuleV2`, framework-dependent `dotnet`/Wasla.Api.dll, in-process hosting; no custom handler edits. |
| Saved health OpenAPI schema | Matches actual Swagger captured by the final contract test. |
| PowerShell scripts, local documentation links, changed-file inventory and Git whitespace | Syntax/link/inventory checks passed; final `git diff --check` also passed. Production HTTP smoke script is provided, not claimed executed on IIS. |
| Full solution regression with isolated local SQL test databases | **760 passed, 0 failed, 1 skipped** in 41m24s: unit 348, integration 386 + 1 opt-in browser skip, architecture 26. The skipped `ExportBrowserSmokeTests` case passed separately in both final actual-browser runs. |

The first browser run passed, but visual/dimension QA exposed the original landscape CSS defect; final results above use the corrected template and an assertion on actual PDF geometry. Final API/unit checks also preserve the original report `Rejected` audit/byte count rather than changing oversized reports to infrastructure failures. An early unrestricted-parallel unit attempt failed in a byte-limit test that also performed an unnecessary Razor compilation; that test now isolates byte-limit behavior, and the full final unit suite passed with the repository's bounded test parallelism.


Broad-run TRX files: `regression_net10.0_20261029154514.trx` (348 unit), `regression_net10.0_20261029162606.trx` (386 integration passed / browser opt-in skipped), `regression_net10.0_20261029162618.trx` (26 architecture). The two newly added PDF/XLSX byte-limit API cases are included in the 10-case final API run. JSON counters/assembly identities are retained in `_local-data/export-runtime/test-summary.json`.

Latest synthetic artifact QA (`_local-data/export-runtime/qa-final/`):

| Artifact | Actual result |
|---|---|
| `report-ar.pdf` | 20 A4 portrait pages, 1,137,268 bytes, 595.92 × 841.92 points. |
| `report-en-landscape.pdf` | 29 A4 landscape pages, 1,136,545 bytes, 841.92 × 595.92 points. |
| `prescription-Finalized.pdf` | 1 A4 page, 61,936 bytes; Finalized/version/footer markings. |
| `prescription-Superseded.pdf` | 1 A4 page, 78,010 bytes; historical status, invalid-current-treatment warning and watermark. |
| `prescription-Voided.pdf` | 1 A4 page, 78,108 bytes; voided warning and watermark. |
| Arabic/English XLSX | 32,910 / 32,736 bytes; each reopened with ClosedXML and independently with openpyxl: 7 expected sheets, 600 details, numeric EGP, correct detail-count metadata and Arabic RTL. |

Poppler-rendered first/middle/last pages of both reports and all prescription pages were visually inspected: readable Arabic shaping/mixed-direction text, encoded script-like detail text, repeated table headings, chart, margins and no observed clipping/overlap. pypdf checked all page sizes and every `pageNumber / totalPages` footer. No real medical data was used. These local artifacts are QA evidence, not production clinical documents.

Executed command families (final isolated output folders avoid changing binaries used by the concurrently running broad regression):

```powershell
dotnet build Wasla.sln -c Release --no-restore
dotnet publish src/Wasla/Wasla.Api/Wasla.Api.csproj -c Release --no-restore -o E:/Wasla/_local-data/export-runtime/publish
dotnet test Wasla.sln -c Release --no-build --no-restore -m:1 --logger 'trx;LogFilePrefix=regression' --results-directory _local-data/export-runtime/results -v normal -- xUnit.MaxParallelThreads=2
dotnet build tests/Wasla.Tests.Unit -c Release --no-restore -o E:/Wasla/_local-data/export-runtime/final-unit
dotnet test E:/Wasla/_local-data/export-runtime/final-unit/Wasla.Tests.Unit.dll --logger 'trx;LogFileName=final-unit.trx' --results-directory E:/Wasla/_local-data/export-runtime/results -- xUnit.MaxParallelThreads=2
dotnet test tests/Wasla.Tests.Architecture -c Release --no-restore --output E:/Wasla/_local-data/export-runtime/final-architecture --logger 'trx;LogFileName=final-architecture.trx' --results-directory E:/Wasla/_local-data/export-runtime/results
dotnet build tests/Wasla.Tests.Integration -c Release --no-restore -o E:/Wasla/_local-data/export-runtime/final-integration
# Set WASLA_RUN_BROWSER_TESTS=1 and the published/QA directories; remove executable overrides.
dotnet test E:/Wasla/_local-data/export-runtime/final-integration/Wasla.Tests.Integration.dll --filter FullyQualifiedName~ExportBrowserSmokeTests
# Final API filter: Phase18ExportApiTests, DoctorPatientPrescriptionTests, DoctorPatientExportSecurityTests,
# Completed_encounter_stays_readable_while_inprogress_sibling_survives_and_reports_keep_asof, Phase18ContractTests.
```

The broad run used process-only `WASLA_SQLSERVER_CONNECTION_STRING` pointing to local SQL Express with disposable fixture databases. `Wasla.sln` runs the Wasla unit/integration/architecture projects. Final API tests used the same isolated SQL harness plus actual-browser opt-in. No production database was queried/migrated by these checks. The broad run began after the main refactor; final orientation, cleanup and size-audit refinements were checked separately by the final full unit/architecture suites, affected API/SQL cases and actual-browser runs, then the complete solution was rebuilt.

The Release-published API served Swagger HTTP 200 via local Kestrel with startup migration/seeding disabled and an unused test-only SQL database name. No browser cache was created at startup. This checks local process startup, not DB readiness or IIS hosting. A background-process attempt was rejected by automatic approval review as “blocked by policy”; the permitted foreground check completed instead.

Actual IIS execution is **unverified**: `appcmd` cannot read `redirection.config` in this session because of insufficient permissions. No production host, credentials or target Application Pool identity was supplied, and no production deployment/recycle/ACL/network policy change was performed. Target identity download/launch, network denial recovery, installed fonts/dependencies and recycle behavior remain required production checks. Local output cannot establish those facts.

## Complete changed-file inventory

Paths are relative to the repository root; the relocated template is listed at both its old and new paths.

| File | Reason |
|---|---|
| `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Reports/ChromiumBrowserRuntime.cs` | Production BrowserFetcher/Puppeteer adapter and process-wide cancellation-safe download coordinator. |
| `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Reports/ChromiumPdfRenderer.cs` | Shared bounded HTML-to-PDF lifecycle, reference options, signature checks and disposal. |
| `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Reports/PdfRuntimeLog.cs` | Structured stage/type diagnostics without medical/raw exception contents. |
| `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Reports/PrescriptionPdfRenderer.cs` | Delegate issued-version HTML/options to the shared runtime, retain private/status/size behavior. |
| `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Reports/ReportDocumentRenderer.cs` | Physical lazy Razor project, shared PDF runtime and measured health; preserve ClosedXML. |
| `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Reports/ExportRuntimeProbeHostedService.cs` (removed) | Remove mandatory startup browser work. |
| `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Reports/Templates/Report.cshtml` (removed) | Remove stale embedded template source after migration. |
| `src/Wasla/Wasla.Api/Views/Templates/Report.cshtml` | Migrate trusted report layout and honor requested A4 orientation. |
| `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/DependencyInjection.cs` | Register shared singleton browser/PDF runtime; remove hosted probe. |
| `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Properties/AssemblyInfo.cs` | Expose internal download coordination to deterministic unit tests. |
| `src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer/Wasla.Infrastructure.EntityFrameworkCore.SqlServer.csproj` | Remove embedded report resource while retaining compile-reference settings. |
| `src/Wasla/Wasla.Api/Wasla.Api.csproj` | Preserve compilation context, copy assemblies and publish physical Razor files. |
| `src/Wasla/Wasla.Application/Features/Reporting/ReportingContracts.cs` | Remove the obsolete executable-path option. |
| `src/Wasla/Wasla.Application/Features/Reporting/ReportExportContracts.cs` | Add automatic-provisioning/download health metadata with existing fields intact. |
| `tests/Wasla.Tests.Unit/ChromiumPdfRendererTests.cs` | Concurrent download/retry/cancellation, reference lifecycle, failures, resource cleanup, limits/health/Excel isolation. |
| `tests/Wasla.Tests.Unit/PdfTestSupport.cs` | Lightweight runtime/interface/environment fakes without a new mocking dependency. |
| `tests/Wasla.Tests.Unit/DoctorPatientPdfTests.cs` | Deterministic encoded status/history and controlled-runtime tests without installed Chrome. |
| `tests/Wasla.Tests.Unit/Phase18ReportExportTests.cs` | Physical template/orientation, bilingual XLSX, full-row/empty, health failure regressions. |
| `tests/Wasla.Tests.Unit/Wasla.Tests.Unit.csproj` | Copy the real API template into the unit test output. |
| `tests/Wasla.Tests.Integration/ExportBrowserSmokeTests.cs` | Opt-in actual-download/published-template PDF/XLSX smoke with real A4 geometry. |
| `tests/Wasla.Tests.Integration/Phase18ExportApiTests.cs` | Deterministic controlled download failure and byte-limit/audit/API regressions. |
| `tests/Wasla.Tests.Integration/DoctorPatientPrescriptionTests.cs` | Keep ordinary historical/status/security API coverage deterministic; use the real runtime when opted in. |
| `tests/Wasla.Tests.Integration/Phase18PartTwoSqlTests.cs` | Replace executable override with opt-in automatic-browser parity checks. |
| `scripts/phase18/Invoke-IisExportSmoke.ps1` | Repeatable authenticated existing-route downloads and structural/header checks. |
| `docs/doctor-patients/verify.ps1` | Remove installed-browser prerequisite; enable explicit automatic-browser tests. |
| `docs/phase18/part1-export-architecture.md` | Document current shared lifecycle, template layout, diagnostics, security and Excel independence. |
| `docs/phase18/part1-openapi.json` | Add the two backward-compatible automatic-browser health properties to the saved schema. |
| `docs/phase18/part1-contract-schemas.md` | Document the additive health fields and legacy executable metadata semantics. |
| `docs/phase18/part2-tests-and-evidence.md` | Update reproduction instructions to automatic-browser opt-in; preserve historical results. |
| `docs/phase18/export-runtime-iis-smoke.md` | Full 13-point production IIS verification, permissions/network/fonts and failure recovery procedure. |
| `docs/phase18/customer-survey-export-runtime-report.md` | Root causes, implementation, file inventory, actual verification and remaining limits. |
| `PHASE-18-PART-ONE-DECISIONS.md` | Supersede the old browser/template technical decision with this approved task. |
| `P18_PART1_IMPLEMENTATION_REPORT.md` | Point to current runtime evidence and update the export architecture description. |
| `P18_PART1_ROLLOUT.md` | Replace fixed-executable rollout with lazy-provisioning/IIS checks. |
| `P18_PART1_EXTERNAL_RELEASE_BLOCKERS.md` | Describe actual target-worker runtime verification prerequisite. |
| `DOCTOR-MY-PATIENTS-V1-IMPLEMENTATION-REPORT.md` | Update prescription deployment/runtime reproduction guidance. |
| `SOURCE-OF-TRUTH-PHASE-18-PART-ONE-ADDENDUM.md` | Update only the explicitly superseded PDF runtime technical description. |
| `SOURCE-OF-TRUTH-DOCTOR-MY-PATIENTS-V1-ADDENDUM.md` | Update only the explicitly superseded PDF renderer technical description. |

`ReportExportService.cs` and `Directory.Packages.props` were inspected and remain unchanged: existing report authorization, release checks, auditing, MIME/filename/limit logic and approved package versions are reused.

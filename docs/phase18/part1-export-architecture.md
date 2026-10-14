# PDF and Excel export architecture

`IReportingQueryService.BuildExportAsync` applies the same definitions, scope, time window, AsOfUtc, calculations and quality as preview. `ReportExportService` assembles authorized labels/logo, renders through `IReportDocumentRenderer`, enforces byte limits, rechecks current access and appends immutable metadata audit before returning binary bytes. A report uses a server allow-listed code, never an arbitrary Razor filename, SQL fragment or client-supplied URL.

CustomerSurvey's approved report data → RazorLight → PuppeteerSharp and ClosedXML patterns were inspected and adapted. Central packages: RazorLight2.3.1, PuppeteerSharp25.10.0, ClosedXML0.105.1. Template `Reports/Templates/Report.cshtml` is an embedded trusted resource and compile references are preserved. The renderer contains no copies of business KPI formulas.

## PDF

- Provision an installed, patched Chromium/Chrome executable and an Arabic-capable system font (Tahoma/Segoe UI/Arial fallback). `Phase18__ChromiumExecutablePath` supplies the absolute path. No application request downloads Chromium.
- Headless browser retains sandbox/web security. No `--no-sandbox`, `--disable-web-security`, remote resource or external chart loading. CSP, request interception, offline mode and disabled JavaScript block external dependencies. Browser/page/profile lifetimes are disposed even on failure.
- Default concurrency2 (validated1–4), timeout45 seconds (validated5–120). A4 portrait/landscape, repeated table headers, page numbering, bilingual/RTL text, safe wrapping, selected local PNG KPI bars, metadata/filter/quality/sample counts, comparisons, trends, breakdowns and authorized detail tables.
- Logo reads only authorized single-Practice private media. PNG/JPEG only, maximum1MiB, dimensions≤4096 each and ≤4million pixels. Invalid/truncated/oversized logos are omitted; SVG/remote logo URLs are rejected.
- Startup probe follows database initialization and records actual readiness/failure/time. `/api/v1/admin/operations/exports/health` requires PlatformJobs.ViewHealth and does not expose executable paths. Missing Chromium or render/timeout failures return controlled503. XLSX does not require Chromium.

## Excel

Seven logical worksheets: `01 Summary`, `02 Filters and Quality`, `03 Trends`, `04 Breakdowns`, `05 Details`, `06 Charts`, `07 Comparisons`. RTL applies to Arabic; headings/fills, freeze panes, AutoFilters, widths, repeated print headings, typed dates/UTC timestamps, numeric EGP and true percentage cells are set with ClosedXML. Leading-zero and formula-like references are literal quoted text, never executable formulas. Charts are local PNGs. Missing values stay blank/explicitly unavailable; no fake zero replaces unknown data. Comparison percentages and absolute percentage-point changes use appropriate numeric formatting.

## Limits and audit

Both formats: maximum366 labeled days. PDF≤5000 detail rows/15MiB. XLSX≤25000 rows/20MiB. Rejected limits return422 `Report.ExportTooLarge`; nothing is silently truncated. A stable ASCII filename contains actor, report code, period and language. Responses set MIME type, Content-Disposition, no-store and nosniff. Unknown format/report/filter is controlled; CSV is unsupported.

Downloads share the existing `reports-export` rate-limit policy:12 requests per authenticated actor per15-minute window, queue0; rejected requests return429. PDF concurrency/timeout additionally bounds browser work. Business formulas are not recalculated in the presentation layer.

Audit captures actor/role, exact Practice/Doctor scope and permission snapshot, report window, applied filters, code, format/language, outcome, failure code, byte count and actual recorded time. It contains no file body, clinical note, patient name, secret or browser path. Failures after model authorization and denied-before-release outcomes are recorded. Requests rejected before a model exists use standard API security/error logging without persisting unauthorized scope data.

Quality assurance uses synthetic600-row Arabic/English samples, actual installed Chrome PDF generation and reopening XLSX with ClosedXML; the25000-row limit and empty workbook are also verified. Visual PDF checks cover first/middle/last A4 pages and Arabic shaping. Workbook styles/types/AutoFilter/panes/embedded chart and literal injection-like references are checked from actual XLSX. The artifact-tool import preview is advisory because it does not preserve every ClosedXML fill/image extent; actual OOXML/ClosedXML styles remain the authority. No real patient/finance data is used in artifact QA.

See `Phase18ReportExportTests`, `Phase18ExportApiTests` and the implementation report for executed results. Production browser/fonts/sandbox hosting still needs environment-specific approval; local success is not that approval.

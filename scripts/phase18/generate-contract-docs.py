"""Generate Phase 18 contracts from the running application's ApiExplorer/Swagger output.

Run Phase18ContractTests with WASLA_CONTRACT_OUTPUT_DIRECTORY first. This script
reads contract metadata only; it never reads application configuration or a database.
"""
import argparse
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument("--contracts-dir", type=Path, default=ROOT / "_local-data/phase18/contracts")
args = parser.parse_args()
source = args.contracts_dir
manifest = json.loads((source / "phase18-contract-manifest.json").read_text(encoding="utf-8-sig"))
swagger = json.loads((source / "openapi-all.json").read_text(encoding="utf-8-sig"))
definitions = json.loads((source / "kpi-definitions.json").read_text(encoding="utf-8-sig"))
reports = json.loads((source / "report-definitions.json").read_text(encoding="utf-8-sig"))
out = ROOT / "docs/phase18"
out.mkdir(parents=True, exist_ok=True)
prior_manifest = out / "part1-endpoint-manifest.json"
if prior_manifest.exists():
    prior = json.loads(prior_manifest.read_text(encoding="utf-8"))
    known_ids = {(e["method"], e["route"]): e["endpointId"] for e in prior}
    next_id = max(int(e["endpointId"].split("-")[1]) for e in prior) + 1
    for endpoint in manifest:
        key = (endpoint["method"], endpoint["route"])
        if key in known_ids: endpoint["endpointId"] = known_ids[key]
        else:
            endpoint["endpointId"] = f"P18-{next_id:03}"
            next_id += 1
schemas = swagger["components"]["schemas"]

def write(path, text):
    path.write_text(text.rstrip() + "\n", encoding="utf-8")

def md(value):
    return str(value).replace("|", "\\|").replace("\n", " ")

def rule(e):
    c, route = e["controller"], e["route"]
    if c == "PatientDashboard": return "P.01–P.08"
    if c == "DoctorInsights": return "D.37–D.42"
    if c == "OperatingFinance": return "D.43–D.48"
    if "Handovers" in c: return "R.18–R.22; Doctor oversight"
    if c == "Investigations": return "S.31–S.35"
    if c in ("PlatformReporting", "SuperAdminReportExports", "ExportRuntimeHealth"): return "S.01–S.29 / S.31–S.35"
    if "Reception" in c: return "R.01–R.17"
    if "patients" in route: return "D.30–D.33"
    if "practice-comparison" in route: return "D.34–D.36"
    if "reservations" in route: return "D.02–D.14"
    if "queue" in route: return "D.15–D.24"
    return "D.01–D.36; report catalog"

def test(e):
    c = e["controller"]
    if "Exports" in c or c == "ExportRuntimeHealth": return "Phase18ExportApiTests; Phase18ReportExportTests"
    if c == "OperatingFinance": return "Phase18OperatingFinanceApiTests; Phase18OperatingFinanceTests; Phase18ReconciliationTests"
    if c == "Investigations" or c == "PlatformReporting": return "Phase18PlatformApiTests; Phase18InvestigationTests; Phase18ReconciliationTests"
    if "Handover" in c or "Reception" in c: return "Phase18ReceptionApiTests; Phase18HandoverTests; Phase18ReconciliationTests"
    if c == "PatientDashboard": return "Phase18PatientDashboardApiTests; existing Phase16 privacy regressions"
    return "Phase18DoctorReportingApiTests; Phase18OperationalAnalyticsTests; Phase18ReconciliationTests"

def scope(e):
    role = e["roles"][0]
    if role == "Doctor": return "Current active approved Doctor and owned Practices; linked Reception belongs to this Doctor. Cross-owner IDs are hidden."
    if role == "Reception": return "Current active Reception, approved active owner, active assignment and exact delegated permission for each Practice; underlying detail permission is checked separately."
    if role == "Patient": return "Linked self PatientId resolved from current account; family membership never broadens this dashboard. Clinical/attachment access uses the existing policies."
    return "Current active SuperAdmin with current database role/permission; metadata or privacy-suppressed platform aggregates only. No patient clinical or Doctor salary/expense detail."

def permission(e):
    c = e["controller"]
    if c == "DoctorReportExports": return "Reports.Doctor.ExportOperational OR Reports.Doctor.ExportFinancial (by code); corresponding preview/source read permissions"
    if c == "ReceptionReportExports": return "Reports.Reception.ExportOperational + preview/source permissions + exact assignment delegation"
    if c == "SuperAdminReportExports": return "Reports.Platform.Export + preview/source metadata permissions"
    if c == "ExportRuntimeHealth": return "PlatformJobs.ViewHealth (service guard)"
    if c == "OperatingFinance" and e["action"] == "FilterOptions": return "FinancialExpenses.ViewOwn OR SalaryPlans.ViewOwn (service guard)"
    p = [s.removeprefix("Permission:") for s in e["policies"]]
    if p:
        if c in ("DoctorHandovers", "ReceptionHandovers") and e["method"] != "GET": p += ["Handovers.Doctor.ViewOwn" if c == "DoctorHandovers" else "Handovers.ViewAssigned"]
        if c == "Investigations" and e["method"] != "GET": p += ["Investigations.View"]
        if c == "DoctorInsights" and e["method"] != "GET": p += ["Insights.Doctor.ViewOwn"]
        if c == "DoctorReporting" and e["action"] == "Expenses": p += ["Reports.Doctor.ViewOwn (report service)"]
        return "; ".join(dict.fromkeys(p))
    role = e["roles"][0]
    return {"Doctor": "Dashboards.Doctor.ViewOwn OR Reports.Doctor.ViewOwn", "Reception": "Dashboards.Reception.ViewAssigned OR Reports.Reception.ViewAssigned", "SuperAdmin": "Dashboards.Platform.View OR Reports.Platform.View"}.get(role, "Dashboards.Patient.ViewOwn") + " (service guard)"

def schema_type(s):
    if "$ref" in s: return s["$ref"].split("/")[-1]
    if "allOf" in s: return " & ".join(schema_type(x) for x in s["allOf"])
    if "oneOf" in s: return " | ".join(schema_type(x) for x in s["oneOf"])
    if s.get("type") == "array": return schema_type(s.get("items", {})) + "[]"
    return s.get("type", "object") + (" / " + s["format"] if "format" in s else "")

def resolved(s):
    if "$ref" in s: return schemas[s["$ref"].split("/")[-1]]
    if "allOf" in s and len(s["allOf"]) == 1: return resolved(s["allOf"][0])
    return s

def sample(s, field="", depth=0):
    s = resolved(s)
    if s.get("enum"): return s["enum"][0]
    if s.get("nullable") and depth > 1: return None
    kind = s.get("type")
    if kind == "array": return [] if depth > 1 else [sample(s.get("items", {}), field, depth + 1)]
    if kind == "object" or "properties" in s:
        if depth > 1: return {}
        return {k: sample(v, k, depth + 1) for k, v in s.get("properties", {}).items()}
    if kind in ("number", "integer"):
        if field == "dueDay": return 31
        if field == "percentage": return 100
        if "page" in field.lower(): return 1 if "number" in field.lower() else 20
        if "amount" in field.lower(): return 650
        return 0
    if kind == "boolean": return False
    if s.get("format") == "uuid": return "11111111-1111-1111-1111-111111111111"
    if s.get("format") == "date": return "2026-10-01"
    if s.get("format") == "date-time": return "2026-10-14T09:00:00Z"
    if field.lower() in ("rowversion", "obligationrowversion"): return "AAAAAAAAB9E="
    if "reason" in field.lower() or field.lower() in ("notes", "outcome"): return "Operational correction reviewed"
    if field.endswith("Ar") or field == "summary": return "مراجعة تشغيلية"
    return "Example" if kind == "string" else None

def operation(e):
    return swagger["paths"][e["route"]][e["method"].lower()]

paths = {}
for e in manifest:
    op = operation(e)
    paths.setdefault(e["route"], {})[e["method"].lower()] = dict(op, **{"x-phase18-endpoint-id": e["endpointId"], "x-business-rules": rule(e)})
reachable = set()
def refs(value):
    if isinstance(value, dict):
        ref = value.get("$ref", "")
        if ref.startswith("#/components/schemas/"):
            name = ref.split("/")[-1]
            if name not in reachable:
                reachable.add(name)
                refs(schemas[name])
        for child in value.values(): refs(child)
    elif isinstance(value, list):
        for child in value: refs(child)
refs(paths)
snapshot = {k: v for k, v in swagger.items() if k not in ("paths", "components")}
snapshot["paths"] = paths
snapshot["components"] = {"schemas": {k: schemas[k] for k in sorted(reachable)}, **{k:v for k,v in swagger["components"].items() if k != "schemas"}}
write(out / "part1-openapi.json", json.dumps(snapshot, ensure_ascii=False, indent=2))
phase18_schema_count = len(reachable)
write(out / "part1-endpoint-manifest.json", json.dumps(manifest, ensure_ascii=False, indent=2))

lines = ["# Phase 18 Part One API changes", "", f"{len(manifest)} NEW endpoints generated from the running application's ApiExplorer and Swagger. Existing endpoints are listed in [the reused contract inventory](docs/phase18/part1-reused-api-contracts.md). Business numbering remains the approved task's numbering; P18 IDs identify contracts without renumbering earlier phases.", "", "The [OpenAPI snapshot](docs/phase18/part1-openapi.json) is the field-level request/response authority, including schema required arrays, nullability, enums, date formats and binary exports. [DTO tables](docs/phase18/part1-contract-schemas.md) make those definitions readable. Domain validation and UI behavior below supplement schema-level validation.", "", "## Common behavior", "", "All JSON success responses use the declared DTO directly with camelCase names and string enum codes. UTC timestamps carry UTC semantics; business dates are yyyy-MM-dd. Lists use items/totalCount/pageNumber/pageSize. No-match lists are empty with zero count; unavailable metrics use null plus DataStatus and a warning, never a fabricated zero.", "", "Send bearer authentication. Every new POST/PUT/PATCH requires Idempotency-Key (nonblank, at most 200 characters); preserve it only when retrying the identical command. A changed payload under the same key conflicts. RowVersion is opaque base64 and required for mutations of existing mutable records, except the first persistence of a computed insight. On 409 refresh detail/actions and ask the user to reconcile changes before submitting with a new key. All financial writes, history and idempotency complete in one transaction.", "", "Report filters: inclusive dates, 1–366 days; default previous 30 calendar days ending today. Practice/Doctor/Segment/VisitType IDs must belong to the authorized scope. ComparePreviousPeriod uses the preceding equal-length window at the same AsOfUtc. AsOfUtc must be UTC and no later than generation time. Financial filters (expenseCategory/receptionId/planId) and operational filters are distinct. Report sortBy=Date|Reference|Status|Amount, sortDirection=asc|desc; finance sortBy=Date|Status|Amount. Report pageSize 1–100 (default20), pageNumber 1–1000000. Options pageSize defaults50, max100 and search length max100; each dimension has an independent total. See exact route parameter tables for applicability.", "", "Errors are localized ProblemDetails with errors[].code/message/type and correlation/trace metadata: 400 malformed binding/header/body, 401 unauthenticated, 403 missing role/permission, 404 hidden scoped ID or unsupported report code, 409 stale version/invalid transition/idempotency conflict, 422 invalid business/filter/oversized export, 429 medical rate limit, 503 required runtime/coordination unavailable. Do not branch on translated messages. No new endpoint grants a permission merely because a button was rendered.", "", "Export: GET with format=pdf|xlsx, language=ar|en and optional landscape; exact same report filters/AsOfUtc. Success is binary with Content-Disposition, no-store and nosniff. PDF ≤5000 rows/15MiB, XLSX ≤25000 rows/20MiB, 366 days. Exceeding limits rejects; no truncation/CSV. Preview and export permissions are independent. Doctor overview exports omit financial metrics without financial export permission. Reception and platform exports never include private outgoing finance. Receipts remain immutable JSON DTOs; report PDF/XLSX is available through the catalog.", "", "AvailableActions is authoritative for UI hints, and the destination command revalidates permission, scope, state and version. Metric drilldown routes preserve normalized filters and AsOfUtc. Financial amount decimals are EGP with at most two places; allocations sum exactly100 (at most four decimal places), belong to the Doctor and are rounded in minor units. Required operational summaries/reasons reject clinical/private content. Full forms and transitions are documented in the UI matrix and domain/state-machine documents.", "", "Examples are synthetic schema specimens. Replace reference IDs with values from authorized options/details, RowVersion with the latest returned token, dates with applicable business dates, and choose a valid transition. They illustrate wire shape, not an executable seed scenario.", "", "## Endpoint index", "", "| ID | Rules | Actor | Method / route | Permission |", "|---|---|---|---|---|"]
for e in manifest:
    lines.append(f"| [{e['endpointId']}](#{e['endpointId'].lower()}) | {rule(e)} | {','.join(e['roles'])} | `{e['method']} {e['route']}` | {permission(e)} |")
for e in manifest:
    op = operation(e); params = op.get("parameters", [])
    body = op.get("requestBody", {}).get("content", {}).get("application/json", {}).get("schema")
    response_content = op["responses"]["200"].get("content", {})
    response_schema = response_content.get("application/json", {}).get("schema")
    if response_schema is None and response_content: response_schema = next(iter(response_content.values())).get("schema")
    mutable = e["method"] in ("POST", "PUT", "PATCH")
    workflow = "Refresh available actions after success; open returned detailRoute or refresh the entity, then fetch history and the affected dashboard/report." if mutable else "Load this step with applied filters; display loading, empty, complete/partial, forbidden and unavailable states. Follow only allowed actions."
    request_path = re.sub(r"\{[^}]+\}", "11111111-1111-1111-1111-111111111111", e["route"])
    request_path = request_path.replace("/reports/11111111-1111-1111-1111-111111111111", "/reports/overview")
    query = []
    if any(p["name"].lower() == "fromdate" for p in params): query += ["fromDate=2026-10-01", "toDate=2026-10-14"]
    if "export" in e["route"]: query += ["format=xlsx", "language=ar"]
    if "drilldown" in e["route"]: query += ["metricCode=" + ("RegisteredDoctors" if e["roles"][0] == "SuperAdmin" else "CheckIns")]
    if query: request_path += "?" + "&".join(query)
    lines += ["", f"## {e['endpointId']}", "", f"**NEW · {rule(e)} · {','.join(e['roles'])} · {e['controller']}.{e['action']}**", "", f"`{e['method']} {e['route']}`", "", f"Permission: {permission(e)}. Scope: {scope(e)}", "", f"UI step: {e['action']} in the {'financial management' if e['controller']=='OperatingFinance' else e['controller'].removesuffix('Controller')} journey. {workflow}", "", "| Input | Location | Required by schema | Type / values |", "|---|---|---|---|"]
    for p in params:
        s = p.get("schema", {})
        lines.append(f"| {p['name']} | {p['in']} | {p.get('required',False)} | {md(schema_type(s))}{'; '+str(s['enum']) if 'enum' in s else ''} |")
    if body: lines.append(f"| request | Body | {op.get('requestBody',{}).get('required',False)} | {schema_type(body)}; see DTO tables and domain constraints |")
    if not params and not body: lines.append("| — | — | — | No input |")
    lines += ["", "Response: **200** `" + schema_type(response_schema or {}) + "`. Nullable members, enums and nested contracts are in the linked DTO tables. " + ("Idempotent replay returns the recorded resource; no duplicate movement/history. " if mutable else "" ) + "Common error and empty-state rules above apply; unavailable actions cannot be inferred from the role name.", "", f"Validation coverage: `Phase18ContractTests` checks this route's typed success contract and mutation header; behavioral suite: `{test(e)}`. These suites cover journeys, not a claim of a distinct end-to-end test for every alias/action.", "", "Example request:", "", "```http", e["method"] + " " + request_path, "Authorization: Bearer <access-token>"]
    if mutable: lines += ["Idempotency-Key: 7ce6cdbd-example-operation", "Content-Type: application/json"]
    if body: lines += ["", json.dumps(sample(body), ensure_ascii=False, indent=2)]
    lines += ["```", "", "Example 200 response shape:", "", "```" + ("json" if "export" not in e["route"] else "text"), json.dumps(sample(response_schema or {}), ensure_ascii=False, indent=2) if "export" not in e["route"] else "<binary PDF or XLSX bytes; Content-Disposition supplies the filename>", "```"]
write(ROOT / "PHASE-18-PART-ONE-API-CHANGES.md", "\n".join(lines))

dto = ["# Phase 18 DTO schema tables", "", "Generated from the checked-in OpenAPI snapshot. Required means schema-level presence; domain rules can additionally require meaningful nonempty values or RowVersion for a transition. Nullable is independent from optional presence. All nested object/array types refer to another table in this document. No production examples or configuration values are included."]
for name in sorted(reachable):
    s = schemas[name]
    dto += ["", "## " + name, ""]
    if "enum" in s: dto += ["Allowed values: `" + " | ".join(map(str, s["enum"])) + "`."]; continue
    dto += ["| Field | Type | Required | Nullable | Constraints/default |", "|---|---|---|---|---|"]
    for name2, prop in s.get("properties", {}).items():
        bounds = {k:prop[k] for k in ("enum","minimum","maximum","maxLength","minLength","pattern","default","readOnly") if k in prop}
        dto.append(f"| {name2} | {md(schema_type(prop))} | {name2 in s.get('required',[])} | {prop.get('nullable',False)} | {md(json.dumps(bounds,ensure_ascii=False)) if bounds else '—'} |")
write(out / "part1-contract-schemas.md", "\n".join(dto))

kpi = ["# Phase 18 KPI dictionary", "", f"{len(definitions)} metric definitions generated from `KpiDefinitionRegistry`. The calculator and query source adapters implement these definitions. Counts are not interchangeable with events, cohorts or ratios. Source absence uses null and explicit status. Platform patient/clinical groups below five and complementary slices are suppressed.", "", "Operational dates use each source Practice time-zone snapshot; cash reports use the explicit report zone. AsOfUtc constrains recorded knowledge separately from effective business dates. Previous-period percentage deltas are percentage points; relative deltas are a distinct field. Recompute combined ratios from numerators and denominators. Time capacity and configured daily patient limits remain separate units; unbounded headcount is not fabricated as minutes."]
for d in definitions:
    kpi += ["", "## " + d["MetricCode"], "", "| Attribute | Definition |", "|---|---|"]
    for field in ("NameAr","NameEn","Purpose","Sources","ActorScope","DateBasis","EligibleStates","Numerator","Denominator","Exclusions","TimeZoneBasis","Completeness","AsOfBehavior","Example","Unit"):
        kpi.append(f"| {field} | {md(d.get(field) or 'Not applicable')} |")
write(out / "part1-kpi-dictionary.md", "\n".join(kpi))

catalog = ["# Part One report catalog", "", "GET the actor-specific catalog to determine current preview/export permissions, delegated scope, allowed filters/groupings, limits and data availability. Static codes below are the allow-list; a code outside the actor's list returns404. API reference and OpenAPI define typed ReportModel/ReportPage schemas.", "", "All reports accept 1–366 inclusive days; AsOfUtc cannot be future. Default window is the last30 calendar days ending today. PDF max5000 rows/15MiB; XLSX max25000 rows/20MiB; zero truncation. Each metric includes numerator, denominator, eligible/excluded counts, quality and permitted drilldown. Missing instrumentation is NotInstrumented; absent historical policy is NotMeasured. The catalog's Partial availability describes mixed metric coverage, not a claim that every current source is incomplete.", "", "| Code | Arabic / English | Actors | Financial | Drilldown family |", "|---|---|---|---|---|"]
roles = {1:"Doctor",2:"Reception",3:"SuperAdmin",4:"Patient"}
for d in reports:
    catalog.append(f"| {d['Code']} | {d['TitleAr']} / {d['TitleEn']} | {', '.join(roles[x] for x in d['Actors'])} | {d['Financial']} | {'Source metadata; underlying detail permissions rechecked' if d['Drilldown'] else 'No individual detail; capability false'} |")
catalog += ["", "Operational filters: fromDate/toDate/practiceId, segmentId, visitTypeId, bookingSource, reservationStatus, ticketStatus, asOfUtc, reportTimeZoneId, grouping, comparePreviousPeriod. Financial filters replace operational dimensions with expenseCategory/receptionId/planId. Grouping Day/Week/Month/Practice plus operational Segment/VisitType/Source/Status. SuperAdmin may select DoctorId; Doctor/Reception cannot expand their ownership through it. Options are paged and searched. Dimension IDs are validated against actual scope.", "", "Doctor operating-finance requires expense, salary obligation and existing revenue-read permission; salary/expense reports require their own read permission. Export additionally requires the appropriate operational/financial export permission. Overview/practice comparison previews can contain permitted financial metrics, while operational exports omit those metrics without financial-export permission. Reception requires matching global and per-assignment grants for report, export and underlying detail access independently. Platform exports expose administrative/public metadata and suppressed aggregates only.", "", "Reservations distinguish creation/schedule/event cohorts and include booking hour/weekday, creator category versus booking source, cancellation initiator/coded reason and conversion funnel. Queue includes operational completion, call cycles, waiting age and priority impacts. Capacity uses immutable policies and exception-adjusted interval unions; daily patient limits are shown independently. Patients are distinct within the owning Doctor, including new/returning/90-day retention. Practice comparisons retain denominators rather than averaging ratios. Handovers use immutable event history. Clinical workflow completion is aggregate metadata with no encounter content.", "", "Finance reports use historical obligation/allocation revisions and immutable settlement/reversal movements. AllOutstandingAsOf ignores the selected due window; OutstandingDueInPeriod includes it. Financial drilldown rows carry signed metricValue contributions; operational ratio drilldowns carry numerator/denominator contributions and source event IDs. Capacity drilldown is one policy-backed contribution per Practice, not invented appointment slots. No individual platform patient drilldown exists."]
write(out / "part1-report-catalog.md", "\n".join(catalog))

all_metadata_path = source / "all-api-manifest.json"
if all_metadata_path.exists():
    current_keys = {(e["method"], e["route"]) for e in manifest}
    selected = []
    for e in json.loads(all_metadata_path.read_text(encoding="utf-8-sig")):
        path = e["route"]
        if (e["method"], path) in current_keys: continue
        relevant = (any(x in path for x in ("/queue", "/tickets", "/reservations", "/revenue/dashboard", "/financial-transactions", "/walk-in-options"))
            or any(path.startswith(x) for x in ("/api/v1/families/mine", "/api/v1/patients/me", "/api/v1/prescriptions/mine", "/api/v1/lab-requests/mine", "/api/v1/lab-results/mine", "/api/v1/radiology-requests/mine", "/api/v1/radiology-results/mine", "/api/v1/follow-up-eligibilities/mine"))
            or path in ("/api/v1/doctors/{id}", "/api/v1/doctor-specialization-requests/{id}", "/api/v1/doctors/me/practices"))
        if relevant and path in swagger["paths"] and e["method"].lower() in swagger["paths"][path]: selected.append(e)
    reused_paths = {}
    rows = ["# Existing APIs reused by Part One", "", "These existing routes retain their original semantics, authorization, DTOs, status codes, idempotency/version requirements and clinical/financial safeguards. No competing command is introduced. This generated inventory combines exact runtime routes with current role/policy metadata; the existing phase documentation remains the business authority. The [reused OpenAPI snapshot](part1-reused-openapi.json) supplies every field, enum, nullable member and response for these commands.", "", "Patient composition invokes self handlers, explicitly resolves self PatientId and keeps family/attachment/medical policies. Reception actions revalidate actual active assignment plus delegated underlying permission. Doctor handover/investigation links do not elevate access to these resources. Public routes still have their existing public semantics; this inventory does not grant private access.", "", "| Method / route | Controller.action | Roles | Permission policy | Classification |", "|---|---|---|---|---|"]
    for e in sorted(selected, key=lambda x:(x["route"],x["method"])):
        roles_text = "; ".join(e["roles"]) or "Existing authorization/scope policy; no role shortcut"
        policy_text = "; ".join(e["policies"]) or "Existing endpoint policy"
        rows.append(f"| `{e['method']} {e['route']}` | {e['controller']}.{e['action']} | {md(roles_text)} | {md(policy_text)} | REUSED |")
        reused_paths.setdefault(e["route"], {})[e["method"].lower()] = swagger["paths"][e["route"]][e["method"].lower()]
    write(out / "part1-reused-api-contracts.md", "\n".join(rows))
    reachable.clear(); refs(reused_paths)
    reused = {k:v for k,v in snapshot.items() if k not in ("paths","components")}
    reused["paths"] = reused_paths
    reused["components"] = {"schemas": {k:schemas[k] for k in sorted(reachable)}, **{k:v for k,v in swagger["components"].items() if k!="schemas"}}
    write(out / "part1-reused-openapi.json", json.dumps(reused, ensure_ascii=False, indent=2))
print(json.dumps({"endpoints":len(manifest),"schemas":phase18_schema_count,"metrics":len(definitions),"reports":len(reports)}))

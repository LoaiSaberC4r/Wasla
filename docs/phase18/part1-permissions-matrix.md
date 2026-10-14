# Part One permissions and scope

New names are appended to `PermissionNames.All`; existing permission positions/SystemPermissionIds are preserved. The seeded default roles receive their actor-appropriate permissions. Existing Reception assignment records do **not** automatically acquire new delegated permissions; the Doctor must explicitly grant them through existing reception management. Token claims alone do not authorize these queries or writes: ReportingAccessGuard resolves current active account, first-login state, actual user type/role, role permissions, profile and owned/assigned scope from persistence.

| Actor / permission family | Permissions | Scope / additional checks |
|---|---|---|
| Doctor dashboard/report | Dashboards.Doctor.ViewOwn; Reports.Doctor.ViewOwn | Approved owning Doctor, owned Practices including authorized historical inactive Practices |
| Doctor export | Reports.Doctor.ExportOperational; Reports.Doctor.ExportFinancial | Independent from preview; financial read permissions still required; permission/scope rechecked before bytes |
| Outgoing expense | FinancialExpenses.ViewOwn; ManageOwn; SettleOwn; ReverseOwn | Same owning Doctor; no Reception or SuperAdmin private expense access |
| Salary | SalaryPlans.ViewOwn; SalaryPlans.ManageOwn; SalaryObligations.ViewOwn; SalarySettlements.RecordOwn; SalarySettlements.ReverseOwn | Same owner Reception and active assignments for salary allocation/generation |
| Doctor handover | Handovers.Doctor.ViewOwn; Handovers.Doctor.ManageOwn | Owned Doctor/Practice; reads plus action permission for mutations |
| Insights | Insights.Doctor.ViewOwn; Insights.Doctor.TrackOwn | Own Practice evidence; Track also requires View |
| Reception operational | Dashboards.Reception.ViewAssigned; Reports.Reception.ViewAssigned; Reports.Reception.ExportOperational | Global grant **and** active assignment grant for the specific Practice; approved active owner |
| Reception handover | Handovers.CreateAssigned; ViewAssigned; AcknowledgeAssigned; CloseAssigned; AnnotateAssigned | Global + exact assignment grant; View additionally required for writes; acknowledgement creator restriction |
| Platform report | Dashboards.Platform.View; Reports.Platform.View; Reports.Platform.Export | Metadata/aggregate scope; clinical and patient identities excluded; financial codes absent |
| Investigation | Investigations.View; Create; Assign; StartReview; Resolve; Close; Reopen | Actual active SuperAdmin role/profile; View plus action permission; typed links need underlying governance/jobs read permission |
| Platform operational metadata | PlatformJobs.ViewHealth; PlatformDataQuality.View; PlatformSecurityMetadata.View | Separate job/export-runtime/data-quality/security read capabilities |
| Patient | Dashboards.Patient.ViewOwn | Current linked self PatientId; each component also needs its existing underlying permission and medical/family access policy |

All 39 exact new strings are published in `Wasla.Domain/Security/Phase18PermissionNames.cs` and the generated OpenAPI endpoint reference. The abbreviated expense/salary suffixes above share their displayed family prefix; clients must use full exact names from the API reference.

Detail access is independent from aggregate reports. Doctor booking/ticket/clinical/collection routes need their existing own-view permissions. Reception needs existing global and assignment-scoped PracticeReservations.View / PracticeTickets.View grants; removing the global grant removes PatientId/detail routes even while aggregate report access remains. Worklist patient names are withheld without underlying view permission. Financial export is never implied by operational export.

Filter/route IDs outside ownership or delegated scope return404 to hide existence. Missing actor-wide permission returns403. A revoked assignment removes access immediately on the next request. All long-running exports recheck current grants before releasing bytes. No response cache stores user-specific reports; controllers use no-store for sensitive downloads/self composition. Existing medical rate limits and access logging remain in effect.

SuperAdmin may navigate a Doctor/specialization metadata detail only with its existing governance detail permission; an investigation link does not elevate authority. Founder role/account protection continues in the existing governance commands. Patient dashboard capabilities report disabled family/clinical access explicitly; they never grant entitlement or private attachment URLs.

Evidence: Phase18ReceptionApiTests (revoked delegation), Phase18ReconciliationTests (underlying global grant revocation and platform suppression), Phase18ExportApiTests (separate financial/export scope), Phase18PatientDashboardApiTests and existing Phase16 authorization regressions. All new endpoint roles, policies and required mutation headers are also inspected by Phase18ContractTests.

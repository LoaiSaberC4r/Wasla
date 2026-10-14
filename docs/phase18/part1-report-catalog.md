# Part One report catalog

GET the actor-specific catalog to determine current preview/export permissions, delegated scope, allowed filters/groupings, limits and data availability. Static codes below are the allow-list; a code outside the actor's list returns404. API reference and OpenAPI define typed ReportModel/ReportPage schemas.

All reports accept 1–366 inclusive days; AsOfUtc cannot be future. Default window is the last30 calendar days ending today. PDF max5000 rows/15MiB; XLSX max25000 rows/20MiB; zero truncation. Each metric includes numerator, denominator, eligible/excluded counts, quality and permitted drilldown. Missing instrumentation is NotInstrumented; absent historical policy is NotMeasured. The catalog's Partial availability describes mixed metric coverage, not a claim that every current source is incomplete.

| Code | Arabic / English | Actors | Financial | Drilldown family |
|---|---|---|---|---|
| overview | نظرة عامة / Overview | Doctor, Reception, SuperAdmin | False | Source metadata; underlying detail permissions rechecked |
| reservations | تحليل الحجوزات / Reservation analytics | Doctor, Reception | False | Source metadata; underlying detail permissions rechecked |
| queue | تحليل الطابور والخدمة / Queue and operational service | Doctor, Reception | False | Source metadata; underlying detail permissions rechecked |
| patients | تحليل نشاط مرضى الطبيب / Doctor patient activity | Doctor | False | Source metadata; underlying detail permissions rechecked |
| practice-comparison | مقارنة العيادات / Practice comparison | Doctor | False | Source metadata; underlying detail permissions rechecked |
| operating-finance | التمويل التشغيلي / Operating financial statement | Doctor | True | Source metadata; underlying detail permissions rechecked |
| expenses | المصروفات التشغيلية / Operating expenses | Doctor | True | Source metadata; underlying detail permissions rechecked |
| salary | الرواتب والالتزامات / Salary obligations and movements | Doctor | True | Source metadata; underlying detail permissions rechecked |
| recurring-expenses | المصروفات الدورية / Recurring expense obligations | Doctor | True | Source metadata; underlying detail permissions rechecked |
| activity | نشاط الاستقبال / Reception activity | Reception | False | Source metadata; underlying detail permissions rechecked |
| doctor-lifecycle | مراحل اعتماد الأطباء / Doctor lifecycle | SuperAdmin | False | Source metadata; underlying detail permissions rechecked |
| adoption | النمو والاستخدام / Growth and adoption | SuperAdmin | False | No individual detail; capability false |
| operations | تشغيل المنصة / Platform operations | SuperAdmin | False | No individual detail; capability false |
| geography | التغطية الجغرافية والتخصصات / Geographic and specialization coverage | SuperAdmin | False | Source metadata; underlying detail permissions rechecked |
| security-metadata | بيانات الحوكمة والأمان / Security governance metadata | SuperAdmin | False | No individual detail; capability false |

Operational filters: fromDate/toDate/practiceId, segmentId, visitTypeId, bookingSource, reservationStatus, ticketStatus, asOfUtc, reportTimeZoneId, grouping, comparePreviousPeriod. Financial filters replace operational dimensions with expenseCategory/receptionId/planId. Grouping Day/Week/Month/Practice plus operational Segment/VisitType/Source/Status. SuperAdmin may select DoctorId; Doctor/Reception cannot expand their ownership through it. Options are paged and searched. Dimension IDs are validated against actual scope.

Doctor operating-finance requires expense, salary obligation and existing revenue-read permission; salary/expense reports require their own read permission. Export additionally requires the appropriate operational/financial export permission. Overview/practice comparison previews can contain permitted financial metrics, while operational exports omit those metrics without financial-export permission. Reception requires matching global and per-assignment grants for report, export and underlying detail access independently. Platform exports expose administrative/public metadata and suppressed aggregates only.

Reservations distinguish creation/schedule/event cohorts and include booking hour/weekday, creator category versus booking source, cancellation initiator/coded reason and conversion funnel. Queue includes operational completion, call cycles, waiting age and priority impacts. Capacity uses immutable policies and exception-adjusted interval unions; daily patient limits are shown independently. Patients are distinct within the owning Doctor, including new/returning/90-day retention. Practice comparisons retain denominators rather than averaging ratios. Handovers use immutable event history. Clinical workflow completion is aggregate metadata with no encounter content.

Finance reports use historical obligation/allocation revisions and immutable settlement/reversal movements. AllOutstandingAsOf ignores the selected due window; OutstandingDueInPeriod includes it. Financial drilldown rows carry signed metricValue contributions; operational ratio drilldowns carry numerator/denominator contributions and source event IDs. Capacity drilldown is one policy-backed contribution per Practice, not invented appointment slots. No individual platform patient drilldown exists.

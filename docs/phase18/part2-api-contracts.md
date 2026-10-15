# API contracts — Phase 18 Part Two

38 numbered contracts retain their IDs: 6 existing extensions and 32 new routes. Two supplemental read-only selectors resolve Annex C permission-only/dependent and inactive-assignment producer gaps; they do not add business authority.

| ID | Method / exact route | Response |
|---|---|---|
| P18.2-01 | `GET /api/v1/practices/{practiceId}/tickets/{ticketId}` | `existing extension` |
| P18.2-02 | `GET /api/v1/tickets/mine/{ticketId}` | `existing extension` |
| P18.2-03 | `GET /api/v1/reception/practices/{practiceId}/reservations/{reservationId}` | `existing extension` |
| P18.2-04 | `GET /api/v1/doctors/me/practices/{practiceId}/reservations/{reservationId}` | `existing extension` |
| P18.2-05 | `GET /api/v1/reservations/mine/{reservationId}` | `existing extension` |
| P18.2-06 | `GET /api/v1/admin/reservations/{reservationId}` | `existing extension` |
| P18.2-07 | `GET /api/v1/doctors/me/practices/{practiceId}/archives/tickets` | `ArchivePage<ArchivedTicketItem>` |
| P18.2-08 | `GET /api/v1/doctors/me/practices/{practiceId}/archives/reservations` | `ArchivePage<ArchivedReservationItem>` |
| P18.2-09 | `GET /api/v1/reception/practices/{practiceId}/archives/tickets` | `ArchivePage<ArchivedTicketItem>` |
| P18.2-10 | `GET /api/v1/reception/practices/{practiceId}/archives/reservations` | `ArchivePage<ArchivedReservationItem>` |
| P18.2-11 | `GET /api/v1/tickets/mine/archived` | `ArchivePage<ArchivedTicketItem>` |
| P18.2-12 | `GET /api/v1/reservations/mine/archived` | `ArchivePage<ArchivedReservationItem>` |
| P18.2-13 | `GET /api/v1/admin/archives/tickets` | `ArchivePage<PlatformTicketMetadata>` |
| P18.2-14 | `GET /api/v1/admin/archives/reservations` | `ArchivePage<PlatformReservationMetadata>` |
| P18.2-15 | `GET /api/v1/admin/archives/tickets/{ticketId}` | `PlatformTicketMetadata` |
| P18.2-16 | `GET /api/v1/admin/archives/operations/health` | `ArchiveHealth` |
| P18.2-17 | `GET /api/v1/admin/archives/operations/runs` | `ArchivePage<ArchiveRunItem>` |
| P18.2-18 | `GET /api/v1/admin/archives/operations/runs/{runId}` | `ArchiveRunItem` |
| P18.2-19 | `GET /api/v1/admin/archives/operations/exceptions` | `ArchivePage<ArchiveExceptionItem>` |
| P18.2-20 | `GET /api/v1/admin/archives/operations/exceptions/{exceptionId}` | `ArchiveExceptionItem` |
| P18.2-21 | `POST /api/v1/admin/archives/operations/pause` | `ArchiveControlResponse` |
| P18.2-22 | `POST /api/v1/admin/archives/operations/resume` | `ArchiveControlResponse` |
| P18.2-23 | `GET /api/v1/doctors/me/practices/{practiceId}/audit/events` | `ArchivePage<SafeAuditEvent>` |
| P18.2-24 | `GET /api/v1/doctors/me/practices/{practiceId}/audit/events/{eventKey}` | `SafeAuditEvent` |
| P18.2-25 | `GET /api/v1/doctors/me/practices/{practiceId}/audit/sensitive-access` | `ArchivePage<SafeAuditEvent>` |
| P18.2-26 | `GET /api/v1/reception/practices/{practiceId}/audit/my-activity` | `ArchivePage<SafeAuditEvent>` |
| P18.2-27 | `GET /api/v1/reception/practices/{practiceId}/audit/my-activity/{eventKey}` | `SafeAuditEvent` |
| P18.2-28 | `GET /api/v1/admin/audit/events` | `ArchivePage<SafeAuditEvent>` |
| P18.2-29 | `GET /api/v1/admin/audit/events/{eventKey}` | `SafeAuditEvent` |
| P18.2-30 | `POST /api/v1/admin/audit/sensitive-access/search` | `ArchivePage<SafeAuditEvent>` |
| P18.2-31 | `GET /api/v1/admin/archives/reservations/{reservationId}` | `PlatformReservationMetadata` |
| P18.2-32 | `GET /api/v1/archives/metadata` | `ArchiveMetadata` |
| P18.2-33 | `GET /api/v1/doctors/me/archives/practices/options` | `OptionsPage<PracticeOption>` |
| P18.2-34 | `GET /api/v1/admin/archives/practices/options` | `OptionsPage<PracticeOption>` |
| P18.2-35 | `GET /api/v1/admin/archives/operations/filter-options` | `ArchiveOperationsOptions` |
| P18.2-36 | `GET /api/v1/doctors/me/practices/{practiceId}/audit/filter-options` | `AuditOptions` |
| P18.2-37 | `GET /api/v1/reception/practices/{practiceId}/audit/filter-options` | `AuditOptions` |
| P18.2-38 | `GET /api/v1/admin/audit/filter-options` | `AuditOptions` |

Supplemental selectors: `GET /api/v1/reservations/mine/archived/patients/options?pageNumber=1&pageSize=20` returns permitted self/dependent `patientId` under archive-own + existing dependent-view/relationship policy, without requiring booking or family-management permission. `GET /api/v1/reception/archives/practices/options` provides current delegated archive/audit Practice IDs including historical inactive contexts. Existing `/auth/me`, reception contexts, assignable permissions, patient search and family sources remain reusable.

ArchivePage always contains items, nextCursor, pageSize, asOfUtc and hasMore. IDs are ticketId/reservationId, with doctorPracticeId, original businessDate/number/reference, preserved status, archivedAtUtc and detailsRoute. Platform DTOs omit patient/clinical/private financial data. Search supports original business-date ranges, exact status code, Ticket number or Reservation reference, and scoped staff search/patientId. Patient Ticket scope is self-only; Patient Reservation patientId selects only a currently entitled subject. Page size 1–100. Cursors are encrypted/authenticated and bound to actor, current scope/permissions and filters, deterministic businessDate/ID ordering, UTC snapshot and committed movement watermark. Backfill after the first page is outside that page set, including under equal test-clock timestamps.

Existing Ticket detail retains old fields and adds isArchived, archivedAtUtc, operationalCapabilities, typed redacted timeline, timelineHasMore and patientsAheadNowIsCurrent. Archived PatientsAheadNow retains the compatible zero integer with isCurrent=false; no historical queue position is claimed. Operational capabilities explicitly include canCheckIn=false; finalized live NoShow also disables restore before physical movement. Nullable referenced capability/enum schemas use an explicit null alternative under the [OpenAPI 3.0.4 schema rules](https://spec.openapis.org/oas/v3.0.4.html), and nested timeline/capability non-null fields are required. Archive operations are disabled; independent CanRefund and financial/clinical navigation use their separate rights. Reservation detail retains old snapshots/capabilities and adds isArchived, archivedAtUtc, linkedTicketId, canViewLinkedTicket; archived mutation blocked reasons are Archive.ReadOnly. P18.2-06 keeps its administrative gate and active response; archived results use the safe platform metadata representation (same ID/route), while P18.2-31 needs no unrelated administrative Reservation grant.

Health produces control.state/rowVersion/canPause/canResume, paused metadata and readable run IDs. Pause/Resume require ViewHealth plus distinct action permission, nonempty reason (≤1000), current rowVersion and required Idempotency-Key header (≤200). Matching Actor+Operation+key/payload replays the saved result; changed payload conflicts. Resume rechecks actual readiness and feature enablement. Pause waits for an in-flight movement transaction and prevents new starts. No public archive/run/start/backfill/repair/unarchive/purge action exists.

AuditPage uses the same bounded page shape. Every row supplies an opaque stable URL-safe encrypted eventKey, original occurredAtUtc, safe action/entity/outcome, permitted entity/Practice/actor metadata, optional safe failure/correlation/run references. Source identifiers stay backend-only. Details reauthorize current scope; invalid or foreign keys return safe NotFound. Reception sees only own current assigned activity. Restricted admin search is POST with required reason and explicit non-default permission; no clinical body or Patient identity is returned. Patient access log remains the existing MedicalRecords.ViewOwnAccessLog API.

Options carry exact code/nameAr/nameEn. Practice and staff actor selectors are paged/searchable. All dates use original Practice-local businessDate; archived/audit timestamps are UTC. No UI-computed IDs, history source keys or enums are required.

Reservation timelines are bounded to 200 entries with `timelineHasMore`; source events remain retained. The static `supportedFilters` array contains exact query keys. `reference` applies to Reservations and `ticketNumber` to Tickets; platform search/patientId and Patient Ticket subject overrides are rejected. Sensitive-only audit keys do not confer generic detail access. `part2-openapi.json` is the generated handoff snapshot for 38 numbered routes plus two supplemental selectors, including required/nullable fields, Cache-Control and Retry-After headers. The two supplemental selector signatures are documented above and in the controller.

Errors follow actual Wasla Result/ProblemDetails: 400 malformed binding/header/body; **422** business input validation (Archive.InvalidFilter/InvalidCursor); 401 unauthenticated; 403 actor-wide missing grant; 404 missing/out-of-scope/revoked target; 409 Archive.ConcurrencyConflict/IdempotencyConflict or existing business state; 429 throttling; 503 genuine persistence/readiness unavailability. Sensitive responses are no-store. Keys/cursors must be treated as opaque. Request bodies contain justification, never GET query reasons. The generated OpenAPI and contract tests are the executable authority for nullability, headers and concrete DTOs.

Fake examples: archive row ticketId=11111111-1111-1111-1111-111111111111, doctorPracticeId=22222222-2222-2222-2222-222222222222, businessDate=2026-10-10, ticketNumber=18, status=Completed. `{"reason":"Scheduled test maintenance","rowVersion":"AQIDBAUGBwg="}` with a client-generated Idempotency-Key illustrates a control request; the caller must use the actual GET-produced version, not this example. Invalid cursor → 422 Archive.InvalidCursor; stale version → 409 Archive.ConcurrencyConflict; unrelated subject → safe 404.

Example platform Ticket search response (all values synthetic):

```json
{
  "items": [{
    "ticketId": "11111111-1111-1111-1111-111111111111",
    "doctorPracticeId": "22222222-2222-2222-2222-222222222222",
    "ticketNumber": 18,
    "businessDate": "2026-10-10",
    "status": "Completed",
    "archivedAtUtc": "2026-10-11T07:00:00Z",
    "detailsRoute": "/api/v1/admin/archives/tickets/11111111-1111-1111-1111-111111111111",
    "isArchived": true
  }],
  "nextCursor": null,
  "pageSize": 20,
  "asOfUtc": "2026-10-15T07:00:00Z",
  "hasMore": false
}
```

An option is `{"code":"Completed","nameAr":"مكتمل","nameEn":"Completed"}`. When hasMore=true, copy the actual nextCursor into the next GET's cursor parameter and retain the identical filters. Cursor values and audit event keys are opaque signed/encrypted runtime values; no fixed fake token is a working credential.

Example stale control error (synthetic trace/instance omitted):

```json
{
  "type": "about:blank#conflict",
  "title": "Conflict",
  "status": 409,
  "errors": [{
    "code": "Archive.ConcurrencyConflict",
    "message": "Refresh the control state / حدث تعارض؛ حدّث الحالة.",
    "type": "Conflict",
    "details": null,
    "source": null,
    "retryAfter": null
  }]
}
```

Responses also carry the standard BuildingBlock instance, traceId/correlationId and localized detail as appropriate. A failed lookup never proves the target exists. A 429 response supplies the actual Retry-After header; the client waits that duration and retains current filters rather than inventing a cursor or rowVersion.

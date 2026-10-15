# Frontend producer → consumer matrix

No Angular UI is implemented. All IDs and capabilities are backend-produced and reauthorized on consumption.

| Flow | Producer and exact field | Consumer / boundary |
|---|---|---|
| Doctor archive entry | 33 items[].practiceId (or existing owned Practice list) | 07/08/23/25; no unrelated Practice-management/Reporting requirement |
| Reception archive entry | Existing reception contexts doctorPracticeId/permissionCodes; supplemental historical archive options practiceId | 09/10/26; current exact delegation |
| Patient archive entry | auth/me linked identity, server-derived; supplemental permitted patient options patientId | 11 self only; 12 self/current entitled dependent |
| Platform entry | 34 items[].practiceId | 13/14/19; safe metadata scope |
| Ticket click | 07/09/11 items[].ticketId + doctorPracticeId/detailsRoute | 01/02; archive permission does not read active detail |
| Reservation click | 08/10/12 items[].reservationId + doctorPracticeId/detailsRoute | 03/04/05; current dependent policy rechecked |
| Platform row click | 13 ticketId / 14 reservationId | 15 / 31 dedicated redacted details |
| Converted Reservation → Ticket | 03/04/05 linkedTicketId + canViewLinkedTicket | 01/02; dependent Ticket access denied |
| Ticket → Reservation | Existing detail reservationId | 03/04/05 with independent resource authorization |
| Refund | Staff detail paymentId + canRefund | Existing payments/{paymentId}/refund; separate finance right, Cancelled never-started, one full Refund |
| Clinical navigation | Permitted detail medicalEncounterId | Existing Encounter route; separate clinical permission/visibility |
| Monitoring run | 16 readable currentRunId/lastRunId or 17 items[].runId | 18 |
| Integrity exception | 19 items[].exceptionId | 20 safe metadata |
| Pause/Resume | 16 control.rowVersion, capability booleans; client key and input reason | 21/22 return fresh version; stale 409 → refresh Health |
| Audit navigation | 23/26/28 items[].eventKey | 24/27/29; opaque key, current scope/actor checked |
| Audit selectors | 36/37/38 exact action/entityType/outcome and paged actors[].actorId / practices[].practiceId | Audit filters; Patient actors redacted; no unrelated Archive/Reporting grant |
| Grant archive delegation | Existing assignable-permissions id/code and Reception assignment id/rowVersion | Existing assignment create/update; refresh contexts; old grants unchanged until explicit action |
| Report drilldown | Existing ReportRecord id/detailRoute plus archival marker/capabilities | Current authorized unified detail; no dead physical-store link |
| Patient reservation history | Existing reservations/mine?view=History reservationId | 05; authorized archive subset joins active history once |

Reception/Patient/Doctor mutations never write archived records. Financial and clinical actions remain separately controlled. A history row or investigation link cannot elevate the consumer's permissions. Static metadata, optional selectors, required headers and 422/409 handling must be wired before screens rely on those inputs. Clinical/restricted source rows may intentionally yield safe NotFound under a generic audit detail permission; no inferred PHI capability is created.

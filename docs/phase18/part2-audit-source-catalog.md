# Audit source catalog

The explorer projects authoritative immutable events on demand. It does not copy old events into a generic history table or derive old permission events from current grants. Event bodies exclude notes, diagnoses, prescriptions, attachments, patient names/identities, amounts, booking notes, reasons, raw filter/scope JSON and SQL exceptions. Patient actors are replaced by `PatientRedacted`; scoped staff selectors exclude Patient accounts.

| Backend source | Authoritative store and verified scope | Public safe projection / restrictions |
|---|---|---|
| TicketHistory | `AllTicketHistories` UNION ALL, joined permanent TicketRegistry | Original ID/time, `TicketHistory.<original enum value>`, Ticket ID, Practice and permitted actor. Stable through movement. |
| ReservationHistory | `AllReservationHistories`, joined ReservationRegistry | `ReservationHistory.<original enum value>`, original ID/time and scoped reference. No reason/comment/note. |
| Handover | ReceptionHandoverHistory → ReceptionHandover | Verified Practice; operation metadata only, no annotations. Reception sees own actor events only. |
| Investigation | InvestigationHistory | Platform only; case ID/operation/time/actor, no evidence or findings. |
| DoctorGovernance | DoctorStatusHistory | Platform only; Doctor ID/status-transition code/time/actor, no reason or identity documents. |
| Export | ReportExportAudit | Platform sees SuperAdmin exports only. Doctor/Reception see own exports whose immutable scope snapshot includes the selected Practice. Raw scope/filter JSON never returned. `Completed`→`Succeeded`, `DeniedBeforeRelease`→`Denied`, other outcomes→`Failed`. |
| PaymentCorrection | PaymentCorrectionHistory → Payment | Doctor/Reception scoped metadata; no amount, before/after values or reason. Omitted from platform explorer. |
| Refund / RefundCorrection | Refund, RefundCorrectionHistory → Refund | Scoped operation IDs/time/actor; no private ledger body. Omitted from platform explorer. |
| EncounterAccess | EncounterAuditEvents → MedicalEncounter | Sensitive permission only, owning Practice proof; action metadata, no clinical content. |
| MedicalAccess | MedicalRecordAccessAudit → target Encounter | Sensitive permission only, provable Practice/Encounter; no patient ID/token/reason. |
| TreatmentAccess | MedicalRecordAccessAudit → requesting TicketRegistry, with no target Encounter | Sensitive scoped metadata. Events with neither provable target nor requesting scope are excluded, not guessed. |
| Archive | ArchiveAuditEvents | Observed/revised/invalidated/closed day, finalized NoShow, archive success/failure, pause/resume, required archived staff Ticket read and restricted search evidence. Safe failure codes and correlation/run links only. |
| New governance events | Authenticated RolePermission and Reception assignment/delegation writes captured in their SaveChanges transaction | Permission/delegation operation and role/assignment/Practice ID; no invented preexisting history. System seeding is not represented as a human approval. |

General explorer is operational/administrative metadata. Sensitive sources are available only through P18.2-25 or reasoned P18.2-30. A generic detail route does not elevate a sensitive search key into a general permission: it safely returns NotFound for sensitive-only sources. Sensitive search rows are self-contained redacted metadata; they do not expose clinical consumers. The existing Patient medical access-log API remains authoritative and retains its policy.

Stable URL-safe `eventKey` encrypts source type + immutable event GUID using authenticated AES-GCM. A persistent random per-database 32-byte secret is initialized by migration. Deterministic keyed nonces derive from the unique immutable payload; repeated identical events yield identical URLs. The key reveals neither source table nor SQL primary key and survives application restart/key-ring rotation. Tampering or unknown sources yield safe 404; every resolution rechecks current actor/Practice scope. This is a locator, not an authorization capability.

Paging merges at most pageSize+1 rows from each authorized source, ordered by original UTC/time, SQL Server GUID ordering and source tie-break. Encrypted Data Protection cursors bind filters, actor, current readable Practice IDs and permissions. Production instances must share/persist the Data Protection key ring for interoperable cursors. Histories maintain one logical source prefix across physical locations, so relocation does not create duplicate explorer events.

Required archived staff Ticket-read audit and restricted search audit fail closed: the response is not released if persistence fails. Restricted justification is stored only as a SHA-256 hash; ordinary diagnostic logs do not contain the reason/body or PHI. Audit event writes are immutable through the DbContext; source histories keep their existing guards. Collector retention and a legal disposal policy remain outside this task.

Authenticated action-level 403/404/409 rejections on Part Two routes and unified details record only actor, safe denial/conflict code and ArchiveLookup/ArchiveControl category. They retain no probed entity/Practice ID, query, body or reason, since denial proves no target relationship. This write completes before the response; a persistence failure follows the normal infrastructure error policy. Authentication/role filters that reject before MVC action execution retain their existing security behavior.

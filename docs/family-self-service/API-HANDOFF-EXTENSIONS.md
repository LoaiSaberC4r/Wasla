> Updated by [the closure handoff](API-HANDOFF-CLOSURE.md): dependent phone updates, complete Workspace fields and historical rejected/quarantined receipts are additive changes.

# Additional family contracts — checkpoint

All prefixes below follow `/api/v1`. Endpoints require authenticated actors, JWT
AND live DB permission, server-derived subject scope, and no-store responses.

## Routine profile and finance

Prefix `/families/mine/dependents/{childPatientId}`:

| Method and suffix | Permission | Input/scope |
|---|---|---|
| GET profile-image | DependentPatientProfile.View | exact operational under18, recheck after open |
| GET contacts | same | contacts plus Patient rowVersion |
| POST contacts | DependentPatientProfile.ManageContacts | `{rowVersion,contact}` + Idempotency-Key |
| PUT contacts/{contactId} | same | same; contact must belong to child |
| DELETE contacts/{contactId} | same | `{rowVersion}` + Idempotency-Key |
| PATCH profile | DependentPatientProfile.ManagePhoto | multipart Photo/RowVersion + key; JPEG/PNG |
| GET financial-transactions | DependentFinance.ViewTransactions | exact operational under18; forced child SQL filter |
| GET payments/{paymentId}/receipt | DependentFinance.ViewReceipts | exact child; no staff fields |
| GET refunds/{refundId}/receipt | same | exact child; private free text removed |

Routine writes return `{changeId,contactId?}`. Fetch profile for the new Patient
rowVersion. Key/content mismatch and stale version return409. Contact LinkedPatientId
input is refused. Shared phone numbers are valid. A phone-less child's last usable
primary contact cannot be removed/demoted. Proxy writes are withheld for a child
with a linked account. Identity/email/credentials never change through these routes.
Own financial endpoints retain their original own-only scope.

## Historical documents

Own prefix `/patients/me/historical-documents`; dependent prefix
`/families/mine/dependents/{childPatientId}/historical-documents`:

| Method and suffix | Own / dependent permissions |
|---|---|
| POST prefix | MedicalDocuments.CreateOwn / CreateDependent |
| GET prefix or {documentId} | MedicalDocuments.ViewOwn / ViewDependent |
| GET {documentId}/versions (own only) | MedicalDocuments.ViewOwn |
| POST {documentId}/versions | MedicalDocuments.ReviseOwn / ReviseDependent |
| POST {documentId}/withdraw | MedicalDocuments.WithdrawOwn / WithdrawDependent |
| GET {documentId}/attachments/{attachmentId}/content | corresponding view permission |

Create/revise multipart: Category, DocumentDate?, DateUnknown, ProviderName?, Title?,
PatientNote?, Files[]. Revise additionally requires RowVersion/Reason. Withdraw JSON:
`{rowVersion,reason}`. Mutations require Idempotency-Key. Create201; revise/withdraw200:
`{documentId,versionNumber,source:"PatientProvided",verificationStatus:"Unverified"}`.
Lists and own version lists support pageNumber/pageSize, default20/max100. Version
pages are arrays; document list returns items/totalCount. Source/trust are immutable.
No Doctor/Practice/Encounter/order is fabricated; provider text is self-declared.

Categories: HistoricalPrescription, HistoricalLabReport, HistoricalRadiologyReport,
SurgeryOrDischargeReport, OtherMedicalDocument. DateUnknown is true exactly when
DocumentDate is absent. Future dates are refused. Own create/revise require an
adult; a linked 16–17 subject can view/withdraw their retained own record.

Dependent permissions are independent medical operation checks: signed configured
policy/sharing, current exact Father/Mother medical proof, under16 policy-local date,
no restriction, active linked actor. MedicalRecords.ViewDependent and operational
booking authority never imply a write grant. Dependent reads additionally require
the existing MedicalRecords.ViewDependent read scope, including token/DB permission
checks. Proxy revision/withdrawal is uploader-
owned; proxy downloads expose current versions only. Own subjects can access old
retained versions. Withdrawn items are omitted by default and never downloadable.

Defaults: five files,10MiB/file,500 documents/subject,1GiB retained bytes/subject,
20 writes/actor/day. Retained versions count toward quota. PDF/JPEG/PNG only. Passive
PDF policy refuses actions, forms, embedded files, encryption and object streams,
including escaped name tokens. Images are limited to16,384/side and40M pixels.
BuildingBlock additionally checks filename/MIME/signature/size and scans. Production
cannot disable the real-scanner requirement. Missing/down/error/unsafe scanning
never publishes an available item. Tests that disable scanner requirement explicitly
exercise contracts/storage, not malware protection. Actual daemon/signature health
and decompression-limit configuration remain operational release gates.

Private downloads check exact document/version/attachment/subject, original size and
SHA256, persist metadata-only audit, then reauthorize/recheck availability. They use
attachment disposition with safe original filename and no-store/nosniff. No media key
or public URL is returned. Uncertain commit retains staged files. Automated
reconciliation reports six private modules with durable resumable cursors and
private findings; it never automatically deletes orphan candidates.

Timeline adds HistoricalPatientDocument without EncounterId. Only `all` and new
`historical-documents` include this source. Existing prescriptions/labs/radiology
filters and official counters retain their meanings. Known document date orders at
UTC midnight; unknown date uses original UploadedAtUtc. Revision/withdrawal/scope
changes invalidate cursors. TreatingDoctor timelines never include this source.

## Dependent doctor-ordered diagnostics

Under `/families/mine/dependents/{childPatientId}`, replace lab with radiology:

| Method and suffix | Permission |
|---|---|
| POST lab-requests/{requestId}/submissions | DependentLabSubmissions.Create |
| GET same | DependentLabSubmissions.View |
| POST same/{submissionId}/withdraw | DependentLabSubmissions.Withdraw |

Form: Attachments[], ExternalProviderName?, ExternalReportDate?, PatientNote?. Five
files maximum,10MiB each; no coverage identifiers. Mutations require key; withdrawal
also JSON rowVersion. Request must belong to the exact child and be submittable.
Independent signed medical operation scope is required. Proxy withdrawal is limited
to their own PendingReview submission. Response only exposes submissionId/requestId/
status/rowVersion; writes do not return clinical results. Actual actor UserId and
subject child PatientId remain separate. Existing source Doctor acceptance/rejection
and coverage/version logic remains authoritative. No official result exists before
doctor acceptance. Existing own diagnostic routes remain own-only.

## Intake revisions and workspace

Onboarding resubmission optionally accepts complete revised intake with fresh
evidence/rowVersion. Omit NameAr to retain intake; supplying it requires the complete
DOB/gender/role and either a child phone or usable guardian contact. Role is immutable. Old identity/contact/evidence
snapshots remain retained. Only current reviewed intake applies on approval. Revision
key replay retains the original response even after approval.

Workspace computes contacts/photo, receipts, historical upload and diagnostic
capabilities separately. Identity correction uses its evidence-reviewed governance
workflow and independent capability. Scanner availability may change between capability display and
submission; handle the localized operation failure.

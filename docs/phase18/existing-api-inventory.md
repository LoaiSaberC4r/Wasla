# Existing API inventory

Baseline: f91348fc64441523f5910b70260e26523d63a101. Existing booking/queue/collections/clinical/family/governance APIs are reused unless a specific extension is documented.

| Source controller | Existing route/action metadata | Classification |
|---|---|---|
| `src/Wasla/Wasla.Api/Controllers/TreatingMedicalRecordController.cs:21` | `[Route("api/v{version:apiVersion}/doctors/me/practices/{practiceId:guid}/tickets/{ticketId:guid}/medical-record")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/TreatingMedicalRecordController.cs:25` | `[HttpGet("overview"), ProducesResponseType<DoctorSharedMedicalRecordOverviewResponse>(200)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/TreatingMedicalRecordController.cs:29` | `[HttpGet("timeline"), ProducesResponseType<RecordPage<TimelineEntry>>(200)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/TreatingMedicalRecordController.cs:33` | `[HttpGet("encounters/{encounterId:guid}"), ProducesResponseType<DoctorSharedMedicalRecordEncounterResponse>(200)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/TreatingMedicalRecordController.cs:37` | `[HttpGet("items/{resourceType}/{sourceId:guid}"), ProducesResponseType<DoctorSharedMedicalRecordItemResponse>(200)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/TreatingMedicalRecordController.cs:41` | `[HttpGet("items/{resourceType}/{sourceId:guid}/attachments/{attachmentId:guid}/content")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ClinicalControllers.cs:16` | `[Route("api/v{version:apiVersion}/doctors/me/practices/{practiceId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ClinicalControllers.cs:19` | `[HttpGet("encounters")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ClinicalControllers.cs:25` | `[HttpGet("encounters/{encounterId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ClinicalControllers.cs:29` | `[HttpGet("tickets/{ticketId:guid}/encounter")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ClinicalControllers.cs:33` | `[HttpPatch("encounters/{encounterId:guid}/clinical-notes")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ClinicalControllers.cs:36` | `[HttpPost("encounters/{encounterId:guid}/diagnoses")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ClinicalControllers.cs:43` | `[HttpPut("encounters/{encounterId:guid}/diagnoses/{diagnosisId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ClinicalControllers.cs:47` | `[HttpDelete("encounters/{encounterId:guid}/diagnoses/{diagnosisId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ClinicalControllers.cs:51` | `[HttpPost("encounters/{encounterId:guid}/amendments")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ClinicalControllers.cs:58` | `[HttpGet("encounters/{encounterId:guid}/amendments")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ClinicalControllers.cs:61` | `[HttpPost("encounters/{encounterId:guid}/follow-up-eligibility")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ClinicalControllers.cs:72` | `[Route("api/v{version:apiVersion}/encounters/mine")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ClinicalControllers.cs:78` | `[HttpGet("{encounterId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ClinicalControllers.cs:86` | `[Route("api/v{version:apiVersion}/follow-up-eligibilities/mine")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ClinicalControllers.cs:93` | `[HttpGet("{eligibilityId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ClinicalControllers.cs:101` | `[Route("api/v{version:apiVersion}/reception/practices/{practiceId:guid}/patients/{patientId:guid}/follow-up-eligibilities")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ClinicalControllers.cs:112` | `[Route("api/v{version:apiVersion}/reservations/follow-up-eligibilities/{eligibilityId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ClinicalControllers.cs:115` | `[HttpGet("available-dates")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ClinicalControllers.cs:123` | `[HttpGet("available-slots")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ClinicalControllers.cs:131` | `[HttpGet("booking-options")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/AuthController.cs:16` | `[Route("api/v{version:apiVersion}/auth")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/AuthController.cs:19` | `[HttpPost("login")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/AuthController.cs:24` | `[HttpGet("me")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/AuthController.cs:29` | `[HttpPost("change-password")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/AuthController.cs:36` | `[HttpPost("doctors/register")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/AuthController.cs:63` | `[HttpPost("patients/register")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/AuthController.cs:88` | `[HttpPost("forgot-password/request-otp")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/AuthController.cs:96` | `[HttpPost("forgot-password/verify-otp")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/AuthController.cs:104` | `[HttpPost("forgot-password/reset")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:14` | `[Route("api/v{version:apiVersion}/admin/superadmins")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:19` | `[Permission(PermissionNames.SuperAdminsViewAll)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:30` | `[HttpGet("{superAdminId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:31` | `[Permission(PermissionNames.SuperAdminsViewDetails)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:36` | `[Permission(PermissionNames.SuperAdminsCreate)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:45` | `[HttpPut("{superAdminId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:46` | `[Permission(PermissionNames.SuperAdminsUpdate)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:58` | `[HttpPost("{superAdminId:guid}/activate")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:59` | `[Permission(PermissionNames.SuperAdminsActivate)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:63` | `[HttpPost("{superAdminId:guid}/deactivate")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:64` | `[Permission(PermissionNames.SuperAdminsDeactivate)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:68` | `[HttpDelete("{superAdminId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:69` | `[Permission(PermissionNames.SuperAdminsDelete)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:73` | `[HttpPost("{superAdminId:guid}/restore")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:74` | `[Permission(PermissionNames.SuperAdminsRestore)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:81` | `[Route("api/v{version:apiVersion}/admin")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:85` | `[HttpGet("roles")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:86` | `[Permission(PermissionNames.RolesView)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:90` | `[HttpGet("roles/{roleId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:91` | `[Permission(PermissionNames.RolesView)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:95` | `[HttpGet("permissions")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:96` | `[Permission(PermissionNames.PermissionsView)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:100` | `[HttpGet("roles/{roleId:guid}/permissions")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:101` | `[Permission(PermissionNames.RolesView)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:105` | `[HttpPut("roles/{roleId:guid}/permissions")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/GovernanceController.cs:106` | `[Permission(PermissionNames.RolePermissionsManage)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientMedicalRecordController.cs:21` | `[Route("api/v{version:apiVersion}/patients/me/medical-record")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientMedicalRecordController.cs:25` | `[HttpGet("overview"), ProducesResponseType<PatientSafeMedicalRecordOverviewResponse>(200)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientMedicalRecordController.cs:29` | `[HttpGet("timeline"), ProducesResponseType<RecordPage<TimelineEntry>>(200)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientMedicalRecordController.cs:33` | `[HttpGet("encounters/{encounterId:guid}"), ProducesResponseType<PatientSafeMedicalRecordEncounterResponse>(200)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientMedicalRecordController.cs:37` | `[HttpGet("items/{resourceType}/{sourceId:guid}"), ProducesResponseType<PatientSafeMedicalRecordItemResponse>(200)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientMedicalRecordController.cs:41` | `[HttpGet("items/{resourceType}/{sourceId:guid}/attachments/{attachmentId:guid}/content")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientMedicalRecordController.cs:52` | `[HttpGet("access-log"), ProducesResponseType<RecordPage<PatientSafeAccessLog>>(200)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:15` | `[Route("api/v{version:apiVersion}/admin/drug-catalog-managers")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:24` | `[HttpGet("{id:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:33` | `[HttpPut("{id:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:36` | `[HttpPost("{id:guid}/activate")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:38` | `[HttpPost("{id:guid}/deactivate")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:48` | `[Route("api/v{version:apiVersion}/admin/drug-catalog")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:57` | `[HttpGet("{drugId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:66` | `[HttpPut("{drugId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:69` | `[HttpPost("{drugId:guid}/activate")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:72` | `[HttpPost("{drugId:guid}/deactivate")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:75` | `[HttpPost("{sourceDrugId:guid}/merge")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:78` | `[HttpPost("imports/preview"), RequestSizeLimit(DrugImportParser.MaximumFileBytes + 1024 * 1024)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:88` | `[HttpGet("imports")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:92` | `[HttpGet("imports/{batchId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:99` | `[HttpGet("imports/{batchId:guid}/changes")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:104` | `[HttpPost("imports/{batchId:guid}/apply")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:117` | `[Route("api/v{version:apiVersion}/doctors/me")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:121` | `[HttpGet("drug-catalog")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:126` | `[HttpGet("drug-catalog-requests")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:131` | `[HttpGet("drug-catalog-requests/{requestId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:137` | `[HttpPost("drug-catalog-requests")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:144` | `[HttpPut("drug-catalog-requests/{requestId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:150` | `[Route("api/v{version:apiVersion}/admin/drug-catalog-requests")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:159` | `[HttpGet("{requestId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:165` | `[HttpPost("{requestId:guid}/request-more-info")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:168` | `[HttpPost("{requestId:guid}/approve")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:171` | `[HttpPost("{requestId:guid}/reject")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:177` | `[Route("api/v{version:apiVersion}/doctors/me/practices/{practiceId:guid}/encounters/{encounterId:guid}/prescription/items")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:185` | `[HttpPut("{itemId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:188` | `[HttpDelete("{itemId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:194` | `[Route("api/v{version:apiVersion}/doctors/me/prescriptions/{prescriptionId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:200` | `[HttpGet("versions")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:203` | `[HttpGet("versions/{versionNumber:int}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:210` | `[HttpGet("correction-draft")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:212` | `[HttpPost("correction-draft")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:215` | `[HttpPost("correction-draft/items")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:218` | `[HttpPut("correction-draft/items/{itemId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:221` | `[HttpDelete("correction-draft/items/{itemId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:224` | `[HttpPost("correction-draft/finalize")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:227` | `[HttpPost("correction-draft/discard")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:230` | `[HttpPost("void")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:236` | `[Route("api/v{version:apiVersion}/prescriptions/mine")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicationControllers.cs:244` | `[HttpGet("{prescriptionId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicalRecordDependentsController.cs:20` | `[Route("api/v{version:apiVersion}/families/mine/medical-record-dependents")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicalCatalogManagersController.cs:15` | `[Route("api/v{version:apiVersion}/admin/medical-catalog-managers")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicalCatalogManagersController.cs:24` | `[HttpGet("{id:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicalCatalogManagersController.cs:33` | `[HttpPut("{id:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicalCatalogManagersController.cs:36` | `[HttpPost("{id:guid}/activate")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/MedicalCatalogManagersController.cs:38` | `[HttpPost("{id:guid}/deactivate")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:75` | `[Route("api/v{version:apiVersion}/admin/lab-catalog")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:83` | `[HttpGet("{id:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:95` | `[HttpPut("{id:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:98` | `[HttpPost("{id:guid}/activate")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:101` | `[HttpPost("{id:guid}/deactivate")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:104` | `[HttpPost("{id:guid}/merge")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:107` | `[HttpPost("imports/preview"), Consumes("multipart/form-data"), RequestSizeLimit(220L * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 210L * 1024 * 1024)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:116` | `[HttpGet("imports"), ProducesResponseType<ClinicalPage<DiagnosticImportBatchResponse>>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:119` | `[HttpGet("imports/{batchId:guid}"), ProducesResponseType<DiagnosticImportBatchResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:122` | `[HttpGet("imports/{batchId:guid}/changes"), ProducesResponseType<ClinicalPage<DiagnosticImportRecordResponse>>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:125` | `[HttpPost("imports/{batchId:guid}/apply"), ProducesResponseType<DiagnosticImportBatchResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:128` | `[HttpPost("imports/{batchId:guid}/discard"), ProducesResponseType<DiagnosticImportBatchResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:134` | `[Route("api/v{version:apiVersion}/admin/lab-catalog-requests")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:141` | `[HttpGet("{requestId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:147` | `[HttpPost("{requestId:guid}/request-more-info")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:150` | `[HttpPost("{requestId:guid}/approve")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:153` | `[HttpPost("{requestId:guid}/reject")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:159` | `[Route("api/v{version:apiVersion}/doctors/me")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:163` | `[HttpGet("lab-catalog"), ProducesResponseType<ClinicalPage<MedicalCatalogResponse>>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:166` | `[HttpGet("lab-catalog-requests"), ProducesResponseType<ClinicalPage<MedicalCatalogRequestResponse>>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:169` | `[HttpGet("lab-catalog-requests/{requestId:guid}"), ProducesResponseType<MedicalCatalogRequestResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:175` | `[HttpPost("lab-catalog-requests"), ProducesResponseType<MedicalCatalogRequestResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:178` | `[HttpPut("lab-catalog-requests/{requestId:guid}"), ProducesResponseType<MedicalCatalogRequestResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:181` | `[HttpGet("practices/{practiceId:guid}/encounters/{encounterId:guid}/lab-request"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:184` | `[HttpPost("practices/{practiceId:guid}/encounters/{encounterId:guid}/lab-request/items"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:187` | `[HttpPut("practices/{practiceId:guid}/encounters/{encounterId:guid}/lab-request/items/{itemId:guid}"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:190` | `[HttpDelete("practices/{practiceId:guid}/encounters/{encounterId:guid}/lab-request/items/{itemId:guid}"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:193` | `[HttpPost("practices/{practiceId:guid}/encounters/{encounterId:guid}/lab-requests/post-visit"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:196` | `[HttpGet("lab-requests"), ProducesResponseType<ClinicalPage<DiagnosticRequestSummary>>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:199` | `[HttpGet("lab-requests/{requestId:guid}"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:202` | `[HttpGet("lab-requests/{requestId:guid}/history"), ProducesResponseType<IReadOnlyList<DiagnosticHistoryResponse>>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:205` | `[HttpPost("lab-requests/{requestId:guid}/items/{itemId:guid}/cancel"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:208` | `[HttpPost("lab-requests/{requestId:guid}/cancel-remaining"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:211` | `[HttpPost("lab-requests/{requestId:guid}/results"), Consumes("multipart/form-data"), RequestSizeLimit(110L * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 105L * 1024 * 1024)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:215` | `[HttpGet("lab-results"), ProducesResponseType<ClinicalPage<DiagnosticResultResponse>>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:218` | `[HttpGet("lab-results/{resultId:guid}"), ProducesResponseType<DiagnosticResultResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:221` | `[HttpGet("lab-results/{resultId:guid}/versions"), ProducesResponseType<IReadOnlyList<DiagnosticVersionResponse>>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:224` | `[HttpGet("lab-results/{resultId:guid}/versions/{versionNumber:int}"), ProducesResponseType<DiagnosticVersionResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:227` | `[HttpPost("lab-results/{resultId:guid}/corrections"), Consumes("multipart/form-data"), RequestSizeLimit(110L * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 105L * 1024 * 1024)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:231` | `[HttpPost("lab-results/{resultId:guid}/void"), ProducesResponseType<DiagnosticResultMutationResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:234` | `[HttpGet("lab-result-submissions"), ProducesResponseType<ClinicalPage<DiagnosticSubmissionSummary>>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:237` | `[HttpGet("lab-result-submissions/{submissionId:guid}"), ProducesResponseType<DiagnosticSubmissionResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:240` | `[HttpPost("lab-result-submissions/{submissionId:guid}/accept"), ProducesResponseType<DiagnosticResultMutationResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:243` | `[HttpPost("lab-result-submissions/{submissionId:guid}/reject"), ProducesResponseType<DiagnosticResultMutationResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:246` | `[HttpGet("lab-results/{resultId:guid}/versions/{versionNumber:int}/attachments/{attachmentId:guid}/content")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:252` | `[HttpGet("lab-result-submissions/{submissionId:guid}/attachments/{attachmentId:guid}/content")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:261` | `[Route("api/v{version:apiVersion}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:265` | `[HttpGet("lab-requests/mine"), ProducesResponseType<ClinicalPage<DiagnosticRequestSummary>>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:268` | `[HttpGet("lab-requests/mine/{requestId:guid}"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:271` | `[HttpGet("lab-results/mine"), ProducesResponseType<ClinicalPage<DiagnosticResultResponse>>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:274` | `[HttpGet("lab-results/mine/{resultId:guid}"), ProducesResponseType<DiagnosticResultResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:277` | `[HttpPost("lab-requests/mine/{requestId:guid}/submissions"), Consumes("multipart/form-data"), RequestSizeLimit(110L * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 105L * 1024 * 1024)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:284` | `[HttpGet("lab-requests/mine/{requestId:guid}/submissions"), ProducesResponseType<ClinicalPage<DiagnosticSubmissionSummary>>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:287` | `[HttpGet("lab-result-submissions/mine/{submissionId:guid}"), ProducesResponseType<DiagnosticSubmissionResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:290` | `[HttpPost("lab-result-submissions/mine/{submissionId:guid}/withdraw"), ProducesResponseType<DiagnosticResultMutationResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:293` | `[HttpGet("lab-result-submissions/mine/{submissionId:guid}/attachments/{attachmentId:guid}/content")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/LabControllers.cs:299` | `[HttpGet("lab-results/mine/{resultId:guid}/attachments/{attachmentId:guid}/content")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/HiddenAccessControllers.cs:22` | `[Route("api/v{version:apiVersion}/doctors/me/practices/{practiceId:guid}/tickets/{ticketId:guid}/medical-record/access-requests")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/HiddenAccessControllers.cs:30` | `[HttpGet("{requestId:guid}"), ProducesResponseType<HiddenAccessResponse>(200)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/HiddenAccessControllers.cs:51` | `[Route("api/v{version:apiVersion}/doctors/me/medical-record-access-requests")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/HiddenAccessControllers.cs:55` | `[HttpGet("inbox/summary"), ProducesResponseType<AccessInboxSummary>(200)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/HiddenAccessControllers.cs:58` | `[HttpGet("inbox"), ProducesResponseType<RecordPage<HiddenAccessResponse>>(200)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/HiddenAccessControllers.cs:62` | `[HttpGet("{requestId:guid}"), ProducesResponseType<HiddenAccessResponse>(200)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/HiddenAccessControllers.cs:65` | `[HttpPost("{requestId:guid}/approve"), ProducesResponseType<HiddenAccessResponse>(200)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/HiddenAccessControllers.cs:69` | `[HttpPost("{requestId:guid}/reject"), ProducesResponseType<HiddenAccessResponse>(200)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/HiddenAccessControllers.cs:73` | `[HttpPost("{requestId:guid}/revoke"), ProducesResponseType<HiddenAccessResponse>(200)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/HiddenAccessControllers.cs:77` | `[HttpGet("{requestId:guid}/history"), ProducesResponseType<IReadOnlyList<RequestHistoryResponse>>(200)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/TicketsController.cs:30` | `[Route("api/v{version:apiVersion}/practices/{practiceId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/TicketsController.cs:34` | `[HttpPost("reservations/{reservationId:guid}/check-in")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/TicketsController.cs:51` | `[HttpPost("reservations/{reservationId:guid}/force-check-in")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/TicketsController.cs:68` | `[HttpPost("tickets/walk-in")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/TicketsController.cs:88` | `[HttpGet("queue")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/TicketsController.cs:93` | `[HttpGet("tickets/{ticketId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/TicketsController.cs:101` | `[HttpPost("queue/call-next")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/TicketsController.cs:110` | `[HttpPost("tickets/{ticketId:guid}/manual-call")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/TicketsController.cs:121` | `[HttpPost("tickets/{ticketId:guid}/recall")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/TicketsController.cs:132` | `[HttpPost("tickets/{ticketId:guid}/confirm-no-response")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/TicketsController.cs:143` | `[HttpPost("tickets/{ticketId:guid}/restore-no-show")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/TicketsController.cs:154` | `[HttpPost("tickets/{ticketId:guid}/start")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/TicketsController.cs:165` | `[HttpPost("tickets/{ticketId:guid}/complete")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/TicketsController.cs:177` | `[HttpPost("tickets/{ticketId:guid}/cancel")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/TicketsController.cs:191` | `[Route("api/v{version:apiVersion}/reception/practices/{practiceId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/TicketsController.cs:195` | `[HttpGet("walk-in/options")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/TicketsController.cs:203` | `[Route("api/v{version:apiVersion}/tickets")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/TicketsController.cs:207` | `[HttpGet("mine/active")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/TicketsController.cs:212` | `[HttpGet("mine/{ticketId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PublicDiscoveryController.cs:17` | `[Route("api/v{version:apiVersion}/public")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PublicDiscoveryController.cs:21` | `[HttpGet("specializations")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PublicDiscoveryController.cs:27` | `[HttpGet("doctors")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PublicDiscoveryController.cs:47` | `[HttpGet("doctors/{doctorId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PublicDiscoveryController.cs:54` | `[HttpGet("doctors/{doctorId:guid}/profile-image")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PublicDiscoveryController.cs:66` | `[HttpGet("practices/{practiceId:guid}/logo")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PublicDiscoveryController.cs:79` | `[HttpGet("practices/{practiceId:guid}/available-dates")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PublicDiscoveryController.cs:91` | `[HttpGet("practices/{practiceId:guid}/available-slots")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PublicDiscoveryController.cs:104` | `[HttpGet("practices/{practiceId:guid}/booking-options")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:15` | `[Route("api/v{version:apiVersion}/reservations")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:19` | `[HttpGet("bookable-patients")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:44` | `[HttpGet("mine")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:58` | `[HttpGet("mine/{reservationId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:63` | `[HttpPost("mine/{reservationId:guid}/cancel")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:76` | `[HttpGet("mine/{reservationId:guid}/reschedule/available-dates")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:90` | `[HttpGet("mine/{reservationId:guid}/reschedule/available-slots")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:106` | `[HttpPost("mine/{reservationId:guid}/reschedule")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:122` | `[Route("api/v{version:apiVersion}/reservations/metadata")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:134` | `[Route("api/v{version:apiVersion}/reception/practices/{practiceId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:138` | `[HttpGet("booking/available-dates")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:144` | `[HttpGet("booking/available-slots")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:155` | `[HttpGet("booking/options")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:167` | `[HttpPost("reservations")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:188` | `[HttpGet("reservations")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:205` | `[HttpGet("reservations/filter-options")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:210` | `[HttpGet("reservations/{reservationId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:218` | `[HttpPost("reservations/{reservationId:guid}/cancel")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:229` | `[HttpGet("reservations/{reservationId:guid}/reschedule/available-dates")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:238` | `[HttpGet("reservations/{reservationId:guid}/reschedule/available-slots")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:248` | `[HttpPost("reservations/{reservationId:guid}/reschedule")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:260` | `[HttpPost("reservations/{reservationId:guid}/restore-no-show")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:274` | `[Route("api/v{version:apiVersion}/doctors/me/practices/{practiceId:guid}/reservations")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:279` | `[Permission(PermissionNames.DoctorPracticeReservationsViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:296` | `[HttpGet("filter-options")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:297` | `[Permission(PermissionNames.DoctorPracticeReservationsViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:302` | `[HttpGet("{reservationId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:303` | `[Permission(PermissionNames.DoctorPracticeReservationsViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:308` | `[HttpPost("{reservationId:guid}/cancel")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:309` | `[Permission(PermissionNames.DoctorPracticeReservationsCancelOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:320` | `[HttpGet("{reservationId:guid}/reschedule/available-dates")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:321` | `[Permission(PermissionNames.DoctorPracticeReservationsRescheduleOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:330` | `[HttpGet("{reservationId:guid}/reschedule/available-slots")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:331` | `[Permission(PermissionNames.DoctorPracticeReservationsRescheduleOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:341` | `[HttpPost("{reservationId:guid}/reschedule")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:342` | `[Permission(PermissionNames.DoctorPracticeReservationsRescheduleOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:357` | `[Route("api/v{version:apiVersion}/admin/reservations")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:359` | `[Permission(PermissionNames.ReservationsViewAdministrative)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/ReservationsController.cs:378` | `[HttpGet("{reservationId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientsController.cs:17` | `[Route("api/v{version:apiVersion}/patients")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientsController.cs:23` | `[Permission(PermissionNames.PatientsRegister)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientsController.cs:37` | `[HttpGet("search")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientsController.cs:38` | `[Permission(PermissionNames.PatientsSearchBasic)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientsController.cs:47` | `[HttpGet("me")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientsController.cs:48` | `[Permission(PermissionNames.PatientProfileViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientsController.cs:52` | `[HttpGet("me/profile-image")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientsController.cs:53` | `[Permission(PermissionNames.PatientProfileViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientsController.cs:62` | `[HttpPut("me")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientsController.cs:64` | `[Permission(PermissionNames.PatientProfileUpdateOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientsController.cs:69` | `[HttpGet("me/contacts")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientsController.cs:70` | `[Permission(PermissionNames.PatientContactsViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientsController.cs:74` | `[HttpPost("me/contacts")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientsController.cs:75` | `[Permission(PermissionNames.PatientContactsManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientsController.cs:82` | `[HttpPut("me/contacts/{contactId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientsController.cs:83` | `[Permission(PermissionNames.PatientContactsManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientsController.cs:87` | `[HttpDelete("me/contacts/{contactId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/PatientsController.cs:88` | `[Permission(PermissionNames.PatientContactsManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:49` | `[Route("api/v{version:apiVersion}/admin/radiology-catalog")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:57` | `[HttpGet("{id:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:69` | `[HttpPut("{id:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:72` | `[HttpPost("{id:guid}/activate")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:75` | `[HttpPost("{id:guid}/deactivate")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:78` | `[HttpPost("{id:guid}/merge")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:81` | `[HttpPost("imports/preview"), Consumes("multipart/form-data"), RequestSizeLimit(220L * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 210L * 1024 * 1024)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:90` | `[HttpGet("imports"), ProducesResponseType<ClinicalPage<DiagnosticImportBatchResponse>>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:93` | `[HttpGet("imports/{batchId:guid}"), ProducesResponseType<DiagnosticImportBatchResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:96` | `[HttpGet("imports/{batchId:guid}/changes"), ProducesResponseType<ClinicalPage<DiagnosticImportRecordResponse>>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:99` | `[HttpPost("imports/{batchId:guid}/apply"), ProducesResponseType<DiagnosticImportBatchResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:102` | `[HttpPost("imports/{batchId:guid}/discard"), ProducesResponseType<DiagnosticImportBatchResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:108` | `[Route("api/v{version:apiVersion}/admin/radiology-catalog-requests")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:115` | `[HttpGet("{requestId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:121` | `[HttpPost("{requestId:guid}/request-more-info")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:124` | `[HttpPost("{requestId:guid}/approve")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:127` | `[HttpPost("{requestId:guid}/reject")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:133` | `[Route("api/v{version:apiVersion}/doctors/me")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:137` | `[HttpGet("radiology-catalog"), ProducesResponseType<ClinicalPage<MedicalCatalogResponse>>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:140` | `[HttpGet("radiology-catalog-requests"), ProducesResponseType<ClinicalPage<MedicalCatalogRequestResponse>>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:143` | `[HttpGet("radiology-catalog-requests/{requestId:guid}"), ProducesResponseType<MedicalCatalogRequestResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:149` | `[HttpPost("radiology-catalog-requests"), ProducesResponseType<MedicalCatalogRequestResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:152` | `[HttpPut("radiology-catalog-requests/{requestId:guid}"), ProducesResponseType<MedicalCatalogRequestResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:155` | `[HttpGet("practices/{practiceId:guid}/encounters/{encounterId:guid}/radiology-request"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:158` | `[HttpPost("practices/{practiceId:guid}/encounters/{encounterId:guid}/radiology-request/items"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:161` | `[HttpPut("practices/{practiceId:guid}/encounters/{encounterId:guid}/radiology-request/items/{itemId:guid}"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:164` | `[HttpDelete("practices/{practiceId:guid}/encounters/{encounterId:guid}/radiology-request/items/{itemId:guid}"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:167` | `[HttpPost("practices/{practiceId:guid}/encounters/{encounterId:guid}/radiology-requests/post-visit"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:170` | `[HttpGet("radiology-requests"), ProducesResponseType<ClinicalPage<DiagnosticRequestSummary>>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:173` | `[HttpGet("radiology-requests/{requestId:guid}"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:176` | `[HttpGet("radiology-requests/{requestId:guid}/history"), ProducesResponseType<IReadOnlyList<DiagnosticHistoryResponse>>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:179` | `[HttpPost("radiology-requests/{requestId:guid}/items/{itemId:guid}/cancel"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:182` | `[HttpPost("radiology-requests/{requestId:guid}/cancel-remaining"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:185` | `[HttpPost("radiology-requests/{requestId:guid}/results"), Consumes("multipart/form-data"), RequestSizeLimit(110L * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 105L * 1024 * 1024)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:189` | `[HttpGet("radiology-results"), ProducesResponseType<ClinicalPage<DiagnosticResultResponse>>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:192` | `[HttpGet("radiology-results/{resultId:guid}"), ProducesResponseType<DiagnosticResultResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:195` | `[HttpGet("radiology-results/{resultId:guid}/versions"), ProducesResponseType<IReadOnlyList<DiagnosticVersionResponse>>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:198` | `[HttpGet("radiology-results/{resultId:guid}/versions/{versionNumber:int}"), ProducesResponseType<DiagnosticVersionResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:201` | `[HttpPost("radiology-results/{resultId:guid}/corrections"), Consumes("multipart/form-data"), RequestSizeLimit(110L * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 105L * 1024 * 1024)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:205` | `[HttpPost("radiology-results/{resultId:guid}/void"), ProducesResponseType<DiagnosticResultMutationResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:208` | `[HttpGet("radiology-result-submissions"), ProducesResponseType<ClinicalPage<DiagnosticSubmissionSummary>>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:211` | `[HttpGet("radiology-result-submissions/{submissionId:guid}"), ProducesResponseType<DiagnosticSubmissionResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:214` | `[HttpPost("radiology-result-submissions/{submissionId:guid}/accept"), ProducesResponseType<DiagnosticResultMutationResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:217` | `[HttpPost("radiology-result-submissions/{submissionId:guid}/reject"), ProducesResponseType<DiagnosticResultMutationResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:220` | `[HttpGet("radiology-results/{resultId:guid}/versions/{versionNumber:int}/attachments/{attachmentId:guid}/content")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:226` | `[HttpGet("radiology-result-submissions/{submissionId:guid}/attachments/{attachmentId:guid}/content")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:235` | `[Route("api/v{version:apiVersion}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:239` | `[HttpGet("radiology-requests/mine"), ProducesResponseType<ClinicalPage<DiagnosticRequestSummary>>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:242` | `[HttpGet("radiology-requests/mine/{requestId:guid}"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:245` | `[HttpGet("radiology-results/mine"), ProducesResponseType<ClinicalPage<DiagnosticResultResponse>>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:248` | `[HttpGet("radiology-results/mine/{resultId:guid}"), ProducesResponseType<DiagnosticResultResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:251` | `[HttpPost("radiology-requests/mine/{requestId:guid}/submissions"), Consumes("multipart/form-data"), RequestSizeLimit(110L * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 105L * 1024 * 1024)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:258` | `[HttpGet("radiology-requests/mine/{requestId:guid}/submissions"), ProducesResponseType<ClinicalPage<DiagnosticSubmissionSummary>>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:261` | `[HttpGet("radiology-result-submissions/mine/{submissionId:guid}"), ProducesResponseType<DiagnosticSubmissionResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:264` | `[HttpPost("radiology-result-submissions/mine/{submissionId:guid}/withdraw"), ProducesResponseType<DiagnosticResultMutationResponse>(StatusCodes.Status200OK)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:267` | `[HttpGet("radiology-result-submissions/mine/{submissionId:guid}/attachments/{attachmentId:guid}/content")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/RadiologyControllers.cs:273` | `[HttpGet("radiology-results/mine/{resultId:guid}/attachments/{attachmentId:guid}/content")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:15` | `[Route("api/v{version:apiVersion}/admin/doctors")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:20` | `[Permission(PermissionNames.DoctorsViewAll)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:31` | `[HttpGet("{doctorId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:32` | `[Permission(PermissionNames.DoctorsViewDetails)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:37` | `[HttpGet("{doctorId:guid}/media/{mediaType}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:38` | `[Permission(PermissionNames.DoctorsViewDetails)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:50` | `[HttpPost("{doctorId:guid}/approve")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:51` | `[Permission(PermissionNames.DoctorsApprove)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:60` | `[HttpPost("{doctorId:guid}/reject")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:61` | `[Permission(PermissionNames.DoctorsReject)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:70` | `[HttpPost("{doctorId:guid}/suspend")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:71` | `[Permission(PermissionNames.DoctorsSuspend)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:80` | `[HttpGet("{doctorId:guid}/suspension-impact")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:81` | `[Permission(PermissionNames.DoctorsSuspend)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:88` | `[HttpPost("{doctorId:guid}/reactivate")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:89` | `[Permission(PermissionNames.DoctorsReactivate)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:101` | `[Route("api/v{version:apiVersion}/doctors/me")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:105` | `[HttpGet("onboarding")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:106` | `[Permission(PermissionNames.DoctorOnboardingViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:111` | `[HttpGet("profile")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:112` | `[Permission(PermissionNames.DoctorProfileViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:117` | `[HttpPut("profile/bio")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:118` | `[Permission(PermissionNames.DoctorProfileUpdateOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:126` | `[HttpGet("qualifications")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:127` | `[Permission(PermissionNames.DoctorProfileViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:132` | `[HttpPost("qualifications")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:133` | `[Permission(PermissionNames.DoctorProfileUpdateOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:145` | `[HttpPut("qualifications/{qualificationId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:146` | `[Permission(PermissionNames.DoctorProfileUpdateOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:155` | `[HttpDelete("qualifications/{qualificationId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorsController.cs:156` | `[Permission(PermissionNames.DoctorProfileUpdateOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorReceptionsController.cs:14` | `[Route("api/v{version:apiVersion}/doctors/me/receptions")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorReceptionsController.cs:18` | `[HttpGet("assignable-permissions")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorReceptionsController.cs:19` | `[Permission(PermissionNames.ReceptionAssignmentsManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorReceptionsController.cs:25` | `[Permission(PermissionNames.ReceptionUsersViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorReceptionsController.cs:30` | `[HttpGet("{receptionId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorReceptionsController.cs:31` | `[Permission(PermissionNames.ReceptionUsersViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorReceptionsController.cs:37` | `[Permission(PermissionNames.ReceptionUsersManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorReceptionsController.cs:54` | `[HttpPost("{receptionId:guid}/assignments")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorReceptionsController.cs:55` | `[Permission(PermissionNames.ReceptionAssignmentsManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorReceptionsController.cs:68` | `[HttpPut("{receptionId:guid}/assignments/{assignmentId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorReceptionsController.cs:69` | `[Permission(PermissionNames.ReceptionAssignmentsManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorReceptionsController.cs:79` | `[HttpPost("{receptionId:guid}/assignments/{assignmentId:guid}/activate")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorReceptionsController.cs:80` | `[Permission(PermissionNames.ReceptionAssignmentsManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorReceptionsController.cs:90` | `[HttpPost("{receptionId:guid}/assignments/{assignmentId:guid}/deactivate")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorReceptionsController.cs:91` | `[Permission(PermissionNames.ReceptionAssignmentsManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorReceptionsController.cs:118` | `[Route("api/v{version:apiVersion}/reception/practices")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:17` | `[Route("api/v{version:apiVersion}/doctors/me/practices")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:22` | `[Permission(PermissionNames.DoctorPracticesViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:27` | `[HttpGet("{practiceId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:28` | `[Permission(PermissionNames.DoctorPracticesViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:34` | `[Permission(PermissionNames.DoctorPracticesManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:51` | `[HttpPut("{practiceId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:52` | `[Permission(PermissionNames.DoctorPracticesManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:69` | `[HttpPost("{practiceId:guid}/activate")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:70` | `[Permission(PermissionNames.DoctorPracticesActivateOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:79` | `[HttpPost("{practiceId:guid}/deactivate")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:80` | `[Permission(PermissionNames.DoctorPracticesActivateOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:93` | `[HttpGet("{practiceId:guid}/configuration")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:94` | `[Permission(PermissionNames.DoctorPracticeConfigurationViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:99` | `[HttpPut("{practiceId:guid}/configuration")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:100` | `[Permission(PermissionNames.DoctorPracticeConfigurationManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:120` | `[HttpGet("{practiceId:guid}/branding")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:121` | `[Permission(PermissionNames.DoctorPracticeBrandingViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:126` | `[HttpGet("{practiceId:guid}/branding/logo")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:127` | `[Permission(PermissionNames.DoctorPracticeBrandingViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:136` | `[HttpPut("{practiceId:guid}/branding")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:137` | `[Permission(PermissionNames.DoctorPracticeBrandingManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:150` | `[HttpPost("{practiceId:guid}/branding/logo")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:152` | `[Permission(PermissionNames.DoctorPracticeBrandingManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:163` | `[HttpDelete("{practiceId:guid}/branding/logo")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:164` | `[Permission(PermissionNames.DoctorPracticeBrandingManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:173` | `[HttpGet("{practiceId:guid}/schedule")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:174` | `[Permission(PermissionNames.DoctorPracticeScheduleViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:179` | `[HttpGet("{practiceId:guid}/schedule/effective")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:180` | `[Permission(PermissionNames.DoctorPracticeScheduleViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:188` | `[HttpPost("{practiceId:guid}/schedule/periods")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:189` | `[Permission(PermissionNames.DoctorPracticeScheduleManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:203` | `[HttpPut("{practiceId:guid}/schedule/periods/{periodId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:204` | `[Permission(PermissionNames.DoctorPracticeScheduleManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:217` | `[HttpDelete("{practiceId:guid}/schedule/periods/{periodId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:218` | `[Permission(PermissionNames.DoctorPracticeScheduleManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:230` | `[HttpPost("{practiceId:guid}/schedule/exceptions")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:231` | `[Permission(PermissionNames.DoctorPracticeScheduleManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:245` | `[HttpPut("{practiceId:guid}/schedule/exceptions/{exceptionId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:246` | `[Permission(PermissionNames.DoctorPracticeScheduleManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:259` | `[HttpDelete("{practiceId:guid}/schedule/exceptions/{exceptionId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:260` | `[Permission(PermissionNames.DoctorPracticeScheduleManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:272` | `[HttpGet("{practiceId:guid}/segments")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:273` | `[Permission(PermissionNames.DoctorPracticeSegmentsViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:278` | `[HttpPost("{practiceId:guid}/segments")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:279` | `[Permission(PermissionNames.DoctorPracticeSegmentsManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:293` | `[HttpPut("{practiceId:guid}/segments/{segmentId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:294` | `[Permission(PermissionNames.DoctorPracticeSegmentsManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:305` | `[HttpGet("{practiceId:guid}/visit-types")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:306` | `[Permission(PermissionNames.DoctorPracticePricingViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:311` | `[HttpPut("{practiceId:guid}/visit-types/{visitTypeId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:312` | `[Permission(PermissionNames.DoctorPracticePricingManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:322` | `[HttpGet("{practiceId:guid}/prices")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:323` | `[Permission(PermissionNames.DoctorPracticePricingViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:328` | `[HttpPost("{practiceId:guid}/prices")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:329` | `[Permission(PermissionNames.DoctorPracticePricingManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:342` | `[HttpPut("{practiceId:guid}/prices/{priceId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:343` | `[Permission(PermissionNames.DoctorPracticePricingManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:353` | `[HttpDelete("{practiceId:guid}/prices/{priceId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorPracticesController.cs:354` | `[Permission(PermissionNames.DoctorPracticePricingManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:15` | `[Route("api/v{version:apiVersion}/admin/medical-specializations")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:20` | `[Permission(PermissionNames.SpecializationsView)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:31` | `[HttpGet("{id:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:32` | `[Permission(PermissionNames.SpecializationsView)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:37` | `[Permission(PermissionNames.SpecializationsCreate)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:41` | `[HttpPut("{id:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:42` | `[Permission(PermissionNames.SpecializationsUpdate)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:56` | `[HttpPost("{id:guid}/activate")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:57` | `[Permission(PermissionNames.SpecializationsActivate)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:61` | `[HttpPost("{id:guid}/deactivate")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:62` | `[Permission(PermissionNames.SpecializationsDeactivate)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:66` | `[HttpDelete("{id:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:67` | `[Permission(PermissionNames.SpecializationsDelete)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:71` | `[HttpPost("{id:guid}/restore")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:72` | `[Permission(PermissionNames.SpecializationsRestore)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:87` | `[Route("api/v{version:apiVersion}/doctors/me")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:91` | `[HttpGet("specializations/options")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:92` | `[Permission(PermissionNames.DoctorSpecializationsViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:96` | `[HttpGet("specializations")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:97` | `[Permission(PermissionNames.DoctorSpecializationsViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:101` | `[HttpGet("specialization-request")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:102` | `[Permission(PermissionNames.DoctorSpecializationsViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:106` | `[HttpPost("specialization-request")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:107` | `[Permission(PermissionNames.DoctorSpecializationsSubmitOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:114` | `[HttpPost("specialization-request/resubmit")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:115` | `[Permission(PermissionNames.DoctorSpecializationsResubmitOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:122` | `[HttpGet("specialization-request/history")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:123` | `[Permission(PermissionNames.DoctorSpecializationsViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:127` | `[HttpGet("practice-location")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:128` | `[Permission(PermissionNames.DoctorPracticeLocationViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:132` | `[HttpPut("practice-location")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:133` | `[Permission(PermissionNames.DoctorPracticeLocationManageOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:142` | `[Route("api/v{version:apiVersion}/admin/doctor-specialization-requests")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:147` | `[Permission(PermissionNames.DoctorSpecializationRequestsViewAll)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:158` | `[HttpGet("{requestId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:159` | `[Permission(PermissionNames.DoctorSpecializationRequestsViewDetails)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:164` | `[HttpGet("{requestId:guid}/history")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:165` | `[Permission(PermissionNames.DoctorSpecializationRequestsViewDetails)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:170` | `[HttpPut("{requestId:guid}/specializations")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:171` | `[Permission(PermissionNames.DoctorSpecializationRequestsAdjust)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:180` | `[HttpPost("{requestId:guid}/approve")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:181` | `[Permission(PermissionNames.DoctorSpecializationRequestsApprove)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:186` | `[HttpPost("{requestId:guid}/request-modification")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:187` | `[Permission(PermissionNames.DoctorSpecializationRequestsRequestModification)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:195` | `[HttpPost("{requestId:guid}/reject")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:196` | `[Permission(PermissionNames.DoctorSpecializationRequestsReject)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:207` | `[Route("api/v{version:apiVersion}/public")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:211` | `[HttpGet("governorates")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:215` | `[HttpGet("governorates/{governorateId:int}/cities")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DoctorOnboardingController.cs:219` | `[HttpGet("cities/{cityId:int}/areas")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DependentMedicalRecordController.cs:21` | `[Route("api/v{version:apiVersion}/families/mine/dependents/{dependentPatientId:guid}/medical-record")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DependentMedicalRecordController.cs:25` | `[HttpGet("overview"), ProducesResponseType<PatientSafeMedicalRecordOverviewResponse>(200)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DependentMedicalRecordController.cs:29` | `[HttpGet("timeline"), ProducesResponseType<RecordPage<TimelineEntry>>(200)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DependentMedicalRecordController.cs:33` | `[HttpGet("encounters/{encounterId:guid}"), ProducesResponseType<PatientSafeMedicalRecordEncounterResponse>(200)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DependentMedicalRecordController.cs:37` | `[HttpGet("items/{resourceType}/{sourceId:guid}"), ProducesResponseType<PatientSafeMedicalRecordItemResponse>(200)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/DependentMedicalRecordController.cs:41` | `[HttpGet("items/{resourceType}/{sourceId:guid}/attachments/{attachmentId:guid}/content")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamilyMedicalAccessControllers.cs:18` | `[Route("api/v{version:apiVersion}/families/mine/medical-access-requests")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamilyMedicalAccessControllers.cs:22` | `[Permission(PermissionNames.FamilyMedicalAccessRequestsCreateOwn), RequestSizeLimit(110 * 1024 * 1024)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamilyMedicalAccessControllers.cs:31` | `[HttpGet("mine"), ProducesResponseType<FamilyMedicalPage<FamilyMedicalQueueResponse>>(200), Permission(PermissionNames.FamilyMedicalAccessRequestsViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamilyMedicalAccessControllers.cs:35` | `[HttpGet("{requestId:guid}"), ProducesResponseType<FamilyMedicalCaseResponse>(200), Permission(PermissionNames.FamilyMedicalAccessRequestsViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamilyMedicalAccessControllers.cs:38` | `[HttpGet("{requestId:guid}/history"), ProducesResponseType<FamilyMedicalPage<FamilyMedicalHistoryResponse>>(200), Permission(PermissionNames.FamilyMedicalAccessRequestsViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamilyMedicalAccessControllers.cs:41` | `[HttpGet("{requestId:guid}/documents/{documentId:guid}/content"), Produces("application/octet-stream"), ProducesResponseType(200),` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamilyMedicalAccessControllers.cs:49` | `[HttpPost("{requestId:guid}/withdraw"), ProducesResponseType<FamilyMedicalCaseResponse>(200), Permission(PermissionNames.FamilyMedicalAccessRequestsWithdrawOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamilyMedicalAccessControllers.cs:52` | `[HttpPost("{requestId:guid}/resubmit"), Consumes("multipart/form-data"), ProducesResponseType<FamilyMedicalCaseResponse>(200),` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamilyMedicalAccessControllers.cs:65` | `[Route("api/v{version:apiVersion}/families/mine/dependents/{childPatientId:guid}/medical-access")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamilyMedicalAccessControllers.cs:77` | `[Route("api/v{version:apiVersion}/admin/family-medical-access-requests")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamilyMedicalAccessControllers.cs:80` | `[HttpGet("summary"), ProducesResponseType<FamilyMedicalSummaryResponse>(200), Permission(PermissionNames.FamilyMedicalAccessRequestsViewAll)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamilyMedicalAccessControllers.cs:86` | `[HttpGet("{requestId:guid}"), ProducesResponseType<FamilyMedicalAdminCaseResponse>(200), Permission(PermissionNames.FamilyMedicalAccessRequestsViewAdminDetails)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamilyMedicalAccessControllers.cs:88` | `[HttpGet("{requestId:guid}/history"), ProducesResponseType<FamilyMedicalPage<FamilyMedicalAdminHistoryResponse>>(200), Permission(PermissionNames.FamilyMedicalAccessRequestsViewAdminDetails)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamilyMedicalAccessControllers.cs:91` | `[HttpGet("{requestId:guid}/documents/{documentId:guid}/content"), Produces("application/octet-stream"), ProducesResponseType(200),` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamilyMedicalAccessControllers.cs:98` | `[HttpPost("{requestId:guid}/approve"), ProducesResponseType<FamilyMedicalCaseResponse>(200), Permission(PermissionNames.FamilyMedicalAccessRequestsDecide)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamilyMedicalAccessControllers.cs:101` | `[HttpPost("{requestId:guid}/reject"), ProducesResponseType<FamilyMedicalCaseResponse>(200), Permission(PermissionNames.FamilyMedicalAccessRequestsDecide)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamilyMedicalAccessControllers.cs:104` | `[HttpPost("{requestId:guid}/request-modification"), ProducesResponseType<FamilyMedicalCaseResponse>(200), Permission(PermissionNames.FamilyMedicalAccessRequestsRequestModification)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamilyMedicalAccessControllers.cs:113` | `[Route("api/v{version:apiVersion}/admin/family-medical-eligibilities")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamilyMedicalAccessControllers.cs:116` | `[HttpGet("reviews"), ProducesResponseType<FamilyMedicalPage<FamilyMedicalEligibilityQueueResponse>>(200), Permission(PermissionNames.FamilyMedicalAccessEligibilityReview)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamilyMedicalAccessControllers.cs:120` | `[HttpGet("{reviewId:guid}"), ProducesResponseType<FamilyMedicalEligibilityResponse>(200), Permission(PermissionNames.FamilyMedicalAccessEligibilityReview)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamilyMedicalAccessControllers.cs:122` | `[HttpPost("{reviewId:guid}/approve"), ProducesResponseType<FamilyMedicalEligibilityResponse>(200), Permission(PermissionNames.FamilyMedicalAccessEligibilityDecide)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamilyMedicalAccessControllers.cs:125` | `[HttpPost("{reviewId:guid}/reject"), ProducesResponseType<FamilyMedicalEligibilityResponse>(200), Permission(PermissionNames.FamilyMedicalAccessEligibilityDecide)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamilyMedicalAccessControllers.cs:128` | `[HttpPost("{reviewId:guid}/revoke"), ProducesResponseType<FamilyMedicalEligibilityResponse>(200), Permission(PermissionNames.FamilyMedicalAccessEligibilityRevoke)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:16` | `[Route("api/v{version:apiVersion}/families")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:20` | `[HttpGet("mine"), ProducesResponseType<MyFamilyResponse>(200)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:21` | `[Permission(PermissionNames.FamiliesViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:28` | `[Route("api/v{version:apiVersion}/family-relationship-requests")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:34` | `[Permission(PermissionNames.FamilyRelationshipRequestsCreate)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:43` | `[HttpGet("mine")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:44` | `[Permission(PermissionNames.FamilyRelationshipRequestsViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:50` | `[HttpGet("{requestId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:51` | `[Permission(PermissionNames.FamilyRelationshipRequestsViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:55` | `[HttpGet("{requestId:guid}/documents/{documentId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:56` | `[Permission(PermissionNames.FamilyRelationshipRequestsViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:60` | `[HttpPost("{requestId:guid}/resubmit")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:62` | `[Permission(PermissionNames.FamilyRelationshipRequestsResubmitOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:81` | `[Route("api/v{version:apiVersion}/family-relationship-requests/assisted")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:87` | `[Permission(PermissionNames.FamilyRelationshipRequestsCreateAssisted)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:98` | `[Permission(PermissionNames.FamilyRelationshipRequestsViewAssisted)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:104` | `[HttpGet("{requestId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:105` | `[Permission(PermissionNames.FamilyRelationshipRequestsViewAssisted)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:109` | `[HttpGet("{requestId:guid}/documents/{documentId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:110` | `[Permission(PermissionNames.FamilyRelationshipRequestsViewAssisted)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:117` | `[HttpPost("{requestId:guid}/resubmit")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:119` | `[Permission(PermissionNames.FamilyRelationshipRequestsResubmitAssisted)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:127` | `[Route("api/v{version:apiVersion}/admin/family-relationship-requests")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:132` | `[Permission(PermissionNames.FamilyRelationshipRequestsViewAll)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:138` | `[HttpGet("{requestId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:139` | `[Permission(PermissionNames.FamilyRelationshipRequestsViewDetails)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:143` | `[HttpGet("{requestId:guid}/documents/{documentId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:144` | `[Permission(PermissionNames.FamilyRelationshipRequestsViewDetails)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:151` | `[HttpPost("{requestId:guid}/approve")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:152` | `[Permission(PermissionNames.FamilyRelationshipRequestsApprove)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:156` | `[HttpPost("{requestId:guid}/reject")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:157` | `[Permission(PermissionNames.FamilyRelationshipRequestsReject)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:161` | `[HttpPost("{requestId:guid}/request-modification")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FamiliesController.cs:162` | `[Permission(PermissionNames.FamilyRelationshipRequestsRequestModification)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/EncounterVisibilityController.cs:22` | `[Route("api/v{version:apiVersion}/doctors/me/practices/{practiceId:guid}/encounters/{encounterId:guid}/visibility")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/EncounterVisibilityController.cs:30` | `[HttpGet("history"), ProducesResponseType<RecordPage<VisibilityHistoryResponse>>(200)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FinanceMutationsController.cs:14` | `[Route("api/v{version:apiVersion}/practices/{practiceId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FinanceMutationsController.cs:18` | `[HttpPost("payments/{paymentId:guid}/refund")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FinanceMutationsController.cs:30` | `[HttpPost("payments/{paymentId:guid}/correct")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FinanceMutationsController.cs:42` | `[HttpPost("refunds/{refundId:guid}/correct")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FinanceReadController.cs:13` | `[Route("api/v{version:apiVersion}/practices/{practiceId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FinanceReadController.cs:17` | `[HttpGet("financial-transactions")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FinanceReadController.cs:23` | `[HttpGet("payments/{paymentId:guid}")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FinanceReadController.cs:29` | `[HttpGet("payments/{paymentId:guid}/receipt")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FinanceReadController.cs:35` | `[HttpGet("refunds/{refundId:guid}/receipt")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FinanceReadController.cs:44` | `[Route("api/v{version:apiVersion}/doctors/me")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FinanceReadController.cs:48` | `[HttpGet("financial-transactions")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FinanceReadController.cs:49` | `[Permission(PermissionNames.DoctorPracticePaymentsViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FinanceReadController.cs:55` | `[HttpGet("revenue/dashboard")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FinanceReadController.cs:56` | `[Permission(PermissionNames.DoctorRevenueViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FinanceReadController.cs:66` | `[Route("api/v{version:apiVersion}/patients/me")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FinanceReadController.cs:68` | `[Permission(PermissionNames.PaymentsViewOwn)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FinanceReadController.cs:71` | `[HttpGet("financial-transactions")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FinanceReadController.cs:77` | `[HttpGet("payments/{paymentId:guid}/receipt")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FinanceReadController.cs:82` | `[HttpGet("refunds/{refundId:guid}/receipt")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FinanceReadController.cs:90` | `[Route("api/v{version:apiVersion}/admin/revenue")]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FinanceReadController.cs:92` | `[Permission(PermissionNames.PlatformRevenueViewAggregates)]` | REUSED |
| `src/Wasla/Wasla.Api/Controllers/FinanceReadController.cs:95` | `[HttpGet("aggregates")]` | REUSED |

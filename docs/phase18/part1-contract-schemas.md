# Phase 18 DTO schema tables

Generated from the checked-in OpenAPI snapshot. Required means schema-level presence; domain rules can additionally require meaningful nonempty values or RowVersion for a transition. Nullable is independent from optional presence. All nested object/array types refer to another table in this document. No production examples or configuration values are included.

## ClinicalPartyResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| id | string / uuid | False | False | — |
| nameAr | string | False | True | — |
| nameEn | string | False | True | — |

## ClinicalTimelineEntry

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| type | string | False | True | — |
| entryId | string | False | True | — |
| occurredAtUtc | string / date-time | False | False | — |
| sourceId | string / uuid | False | False | — |
| encounterId | string / uuid | False | False | — |
| resourceType | string | False | True | — |
| status | string | False | True | — |
| title | string | False | True | — |
| capabilities | TimelineCapabilities | False | False | — |

## DataQualityItem

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| code | string | False | True | — |
| nameAr | string | False | True | — |
| nameEn | string | False | True | — |
| affectedCount | integer / int64 | False | False | — |
| dataStatus | ReportDataStatus | False | False | — |
| dateBasis | string | False | True | — |
| warningCode | string | False | True | — |

## DoctorInsightHistoryResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| id | string / uuid | False | False | — |
| operation | string | False | True | — |
| state | DoctorInsightState | False | False | — |
| actorApplicationUserId | string / uuid | False | False | — |
| recordedAtUtc | string / date-time | False | False | — |
| reason | string | False | True | — |
| sequenceNumber | integer / int32 | False | False | — |

## DoctorInsightHistoryResponseReportPage

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| items | DoctorInsightHistoryResponse[] | False | True | — |
| totalCount | integer / int64 | False | False | — |
| pageNumber | integer / int32 | False | False | — |
| pageSize | integer / int32 | False | False | — |
| dataStatus | ReportDataStatus | False | False | — |
| warnings | string[] | False | True | — |

## DoctorInsightResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| insightId | string / uuid | False | False | — |
| practiceId | string / uuid | False | False | — |
| metricCode | string | False | True | — |
| titleAr | string | False | True | — |
| titleEn | string | False | True | — |
| explanationAr | string | False | True | — |
| explanationEn | string | False | True | — |
| currentWindow | InsightWindow | False | False | — |
| previousWindow | InsightWindow | False | False | — |
| currentValue | number / double | False | False | — |
| previousValue | number / double | False | False | — |
| numerator | number / double | False | False | — |
| denominator | number / double | False | False | — |
| previousNumerator | number / double | False | False | — |
| previousDenominator | number / double | False | False | — |
| sampleCount | integer / int64 | False | False | — |
| asOfUtc | string / date-time | False | False | — |
| evidenceSummaryAr | string | False | True | — |
| evidenceSummaryEn | string | False | True | — |
| dataStatus | ReportDataStatus | False | False | — |
| suggestedActionAr | string | False | True | — |
| suggestedActionEn | string | False | True | — |
| state | DoctorInsightState | False | False | — |
| isPersisted | boolean | False | False | — |
| rowVersion | string | False | True | — |
| availableActions | ReportAction[] | False | True | — |

## DoctorInsightResponseReportPage

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| items | DoctorInsightResponse[] | False | True | — |
| totalCount | integer / int64 | False | False | — |
| pageNumber | integer / int32 | False | False | — |
| pageSize | integer / int32 | False | False | — |
| dataStatus | ReportDataStatus | False | False | — |
| warnings | string[] | False | True | — |

## DoctorInsightState

Allowed values: `New | Acknowledged | Resolved | Dismissed`.

## ExpenseAmountPolicy

Allowed values: `Fixed | Variable`.

## ExpenseCadence

Allowed values: `Monthly | Quarterly | Yearly`.

## ExpenseCategoryCode

Allowed values: `Rent | Utilities | Supplies | Maintenance | Transport | Other`.

## ExportLimits

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| maximumCalendarDays | integer / int32 | False | False | — |
| excelMaximumRows | integer / int32 | False | False | — |
| pdfMaximumRows | integer / int32 | False | False | — |
| excelMaximumBytes | integer / int32 | False | False | — |
| pdfMaximumBytes | integer / int32 | False | False | — |

## ExportRuntimeHealth

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| status | string | False | True | — |
| executableConfigured | boolean | False | False | — |
| lastProbeAtUtc | string / date-time | False | True | — |
| lastProbeSucceeded | boolean | False | True | — |
| failureCode | string | False | True | — |
| maximumConcurrentRenders | integer / int32 | False | False | — |
| timeoutSeconds | integer / int32 | False | False | — |

## FamilyMedicalAccessStatusResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| status | string | False | True | — |
| canView | boolean | False | False | — |
| canRequestRestriction | boolean | False | False | — |
| canRequestRestoration | boolean | False | False | — |
| activeOwnRequestId | string / uuid | False | True | — |
| activeOwnRequestType | FamilyMedicalRequestType | False | False | — |
| unavailableReasonCode | string | False | True | — |

## FamilyMedicalRequestType

Allowed values: `Restriction | Restoration`.

## FamilyMemberResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| patientId | string / uuid | False | False | — |
| nameAr | string | False | True | — |
| nameEn | string | False | True | — |
| role | FamilyMemberRole | False | False | — |
| gender | Gender | False | False | — |
| medicalAccess | FamilyMedicalAccessStatusResponse | False | False | — |

## FamilyMemberRole

Allowed values: `Father | Mother | Child | Guardian | LegalGuardian`.

## FamilyStatus

Allowed values: `Active | Inactive`.

## FinancialTransactionType

Allowed values: `Payment | Refund`.

## FollowUpEligibilityResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| eligibilityId | string / uuid | False | False | — |
| sourceEncounterId | string / uuid | False | False | — |
| patient | ClinicalPartyResponse | False | False | — |
| doctor | ClinicalPartyResponse | False | False | — |
| practice | ClinicalPartyResponse | False | False | — |
| status | FollowUpEligibilityStatus | False | False | — |
| validUntil | string / date | False | False | — |
| reservedReservationId | string / uuid | False | True | — |
| reservedTicketId | string / uuid | False | True | — |
| consumedEncounterId | string / uuid | False | True | — |
| canBook | boolean | False | False | — |
| rowVersion | string | False | True | — |

## FollowUpEligibilityResponseClinicalPage

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| items | FollowUpEligibilityResponse[] | False | True | — |
| totalCount | integer / int64 | False | False | — |
| pageNumber | integer / int32 | False | False | — |
| pageSize | integer / int32 | False | False | — |

## FollowUpEligibilityStatus

Allowed values: `Available | Reserved | Consumed | Expired`.

## Gender

Allowed values: `Male | Female`.

## HandoverActionRequest

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| rowVersion | string | False | True | — |
| outcome | string | False | True | — |
| annotation | string | False | True | — |

## HandoverCreateRequest

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| summary | string | False | True | — |
| reservationId | string / uuid | False | True | — |
| ticketId | string / uuid | False | True | — |
| previousHandoverId | string / uuid | False | True | — |

## HandoverHistoryResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| id | string / uuid | False | False | — |
| operation | string | False | True | — |
| status | HandoverStatus | False | False | — |
| actorApplicationUserId | string / uuid | False | False | — |
| recordedAtUtc | string / date-time | False | False | — |
| note | string | False | True | — |
| sequenceNumber | integer / int32 | False | False | — |

## HandoverHistoryResponseReportPage

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| items | HandoverHistoryResponse[] | False | True | — |
| totalCount | integer / int64 | False | False | — |
| pageNumber | integer / int32 | False | False | — |
| pageSize | integer / int32 | False | False | — |
| dataStatus | ReportDataStatus | False | False | — |
| warnings | string[] | False | True | — |

## HandoverResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| id | string / uuid | False | False | — |
| doctorPracticeId | string / uuid | False | False | — |
| summary | string | False | True | — |
| reservationId | string / uuid | False | True | — |
| ticketId | string / uuid | False | True | — |
| previousHandoverId | string / uuid | False | True | — |
| status | HandoverStatus | False | False | — |
| createdByApplicationUserId | string / uuid | False | False | — |
| createdOnUtc | string / date-time | False | False | — |
| acknowledgedByApplicationUserId | string / uuid | False | True | — |
| acknowledgedOnUtc | string / date-time | False | True | — |
| closedByApplicationUserId | string / uuid | False | True | — |
| closedOnUtc | string / date-time | False | True | — |
| closureOutcome | string | False | True | — |
| rowVersion | string | False | True | — |
| availableActions | ReportAction[] | False | True | — |

## HandoverResponseReportPage

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| items | HandoverResponse[] | False | True | — |
| totalCount | integer / int64 | False | False | — |
| pageNumber | integer / int32 | False | False | — |
| pageSize | integer / int32 | False | False | — |
| dataStatus | ReportDataStatus | False | False | — |
| warnings | string[] | False | True | — |

## HandoverStatus

Allowed values: `Open | Acknowledged | Closed`.

## HiddenCapabilities

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| canRequestAccess | boolean | False | False | — |

## HiddenRequestState

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| status | string | False | True | — |
| requestId | string / uuid | False | True | — |

## HiddenTimelineEntry

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| type | string | False | True | — |
| entryId | string | False | True | — |
| visitDate | string / date | False | False | — |
| isHidden | boolean | False | False | — |
| hiddenEncounterToken | string | False | True | — |
| accessRequest | HiddenRequestState | False | False | — |
| capabilities | HiddenCapabilities | False | False | — |

## InsightTransitionRequest

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| rowVersion | string | False | True | — |
| reason | string | False | True | — |

## InsightWindow

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| fromDate | string / date | False | False | — |
| toDate | string / date | False | False | — |

## InvestigationActionRequest

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| rowVersion | string | False | True | — |
| assignedSuperAdminId | string / uuid | False | True | — |
| resolutionType | InvestigationResolutionType | False | False | — |
| notes | string | False | True | — |

## InvestigationCreateRequest

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| titleAr | string | False | True | — |
| titleEn | string | False | True | — |
| description | string | False | True | — |
| issueType | InvestigationIssueType | False | False | — |
| priority | InvestigationPriority | False | False | — |
| linkedResourceType | InvestigationResourceType | False | False | — |
| linkedResourceId | string / uuid | False | True | — |

## InvestigationFilterOptions

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| statuses | ReportOption[] | False | True | — |
| issueTypes | ReportOption[] | False | True | — |
| priorities | ReportOption[] | False | True | — |
| resolutionTypes | ReportOption[] | False | True | — |
| linkedResourceTypes | ReportOption[] | False | True | — |
| activeSuperAdmins | ReportOption[] | False | True | — |
| totalActiveSuperAdmins | integer / int64 | False | False | — |
| pageNumber | integer / int32 | False | False | — |
| pageSize | integer / int32 | False | False | — |

## InvestigationHistoryResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| id | string / uuid | False | False | — |
| operation | string | False | True | — |
| status | InvestigationStatus | False | False | — |
| actorApplicationUserId | string / uuid | False | False | — |
| assignedSuperAdminId | string / uuid | False | True | — |
| resolutionType | InvestigationResolutionType | False | False | — |
| note | string | False | True | — |
| recordedAtUtc | string / date-time | False | False | — |
| sequenceNumber | integer / int32 | False | False | — |

## InvestigationHistoryResponseReportPage

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| items | InvestigationHistoryResponse[] | False | True | — |
| totalCount | integer / int64 | False | False | — |
| pageNumber | integer / int32 | False | False | — |
| pageSize | integer / int32 | False | False | — |
| dataStatus | ReportDataStatus | False | False | — |
| warnings | string[] | False | True | — |

## InvestigationIssueType

Allowed values: `DoctorVerification | ApprovalIncident | BackgroundJobFailure | OutboxProblem | DataQuality | AdministrativeSupport | SecurityMetadata`.

## InvestigationPriority

Allowed values: `Normal | Urgent`.

## InvestigationResolutionType

Allowed values: `Corrected | Verified | NoActionRequired | InvalidCase`.

## InvestigationResourceType

Allowed values: `None | Doctor | SpecializationRequest | BackgroundJobRun | OutboxMessage`.

## InvestigationResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| id | string / uuid | False | False | — |
| titleAr | string | False | True | — |
| titleEn | string | False | True | — |
| description | string | False | True | — |
| issueType | InvestigationIssueType | False | False | — |
| priority | InvestigationPriority | False | False | — |
| linkedResourceType | InvestigationResourceType | False | False | — |
| linkedResourceId | string / uuid | False | True | — |
| createdByApplicationUserId | string / uuid | False | False | — |
| assignedSuperAdminId | string / uuid | False | True | — |
| status | InvestigationStatus | False | False | — |
| resolutionType | InvestigationResolutionType | False | False | — |
| resolutionNotes | string | False | True | — |
| closureNotes | string | False | True | — |
| createdOnUtc | string / date-time | False | False | — |
| modifiedOnUtc | string / date-time | False | False | — |
| resolvedOnUtc | string / date-time | False | True | — |
| closedOnUtc | string / date-time | False | True | — |
| rowVersion | string | False | True | — |
| availableActions | ReportAction[] | False | True | — |

## InvestigationResponseReportPage

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| items | InvestigationResponse[] | False | True | — |
| totalCount | integer / int64 | False | False | — |
| pageNumber | integer / int32 | False | False | — |
| pageSize | integer / int32 | False | False | — |
| dataStatus | ReportDataStatus | False | False | — |
| warnings | string[] | False | True | — |

## InvestigationStatus

Allowed values: `Open | InReview | Resolved | Closed`.

## JobRunMetadata

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| id | string / uuid | False | False | — |
| startedAtUtc | string / date-time | False | False | — |
| finishedAtUtc | string / date-time | False | True | — |
| status | string | False | True | — |
| failureCode | string | False | True | — |

## MyActiveTicketResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| ticketId | string / uuid | False | False | — |
| ticketNumber | integer / int32 | False | False | — |
| status | TicketStatus | False | False | — |
| doctorPracticeId | string / uuid | False | False | — |
| practiceNameAr | string | False | True | — |
| practiceNameEn | string | False | True | — |
| doctorId | string / uuid | False | False | — |
| doctorNameAr | string | False | True | — |
| doctorNameEn | string | False | True | — |
| patientsAheadNow | integer / int32 | False | False | — |
| lastUpdatedOnUtc | string / date-time | False | False | — |

## MyFamilyResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| familyId | string / uuid | False | False | — |
| status | FamilyStatus | False | False | — |
| members | FamilyMemberResponse[] | False | True | — |

## OperatingActualAmountRequest

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| amount | number / double | False | False | — |
| rowVersion | string | False | True | — |
| reason | string | False | True | — |

## OperatingAllocationResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| practiceId | string / uuid | False | False | — |
| percentage | number / double | False | False | — |
| amount | number / double | False | True | — |

## OperatingCorrectionRequest

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| amount | number / double | False | False | — |
| dueBusinessDate | string / date | False | False | — |
| category | ExpenseCategoryCode | False | False | — |
| descriptionAr | string | False | True | — |
| allocations | PracticeAllocationPercentage[] | False | True | — |
| reason | string | False | True | — |
| rowVersion | string | False | True | — |
| descriptionEn | string | False | True | — |

## OperatingExpenseRequest

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| amount | number / double | False | False | — |
| businessDate | string / date | False | False | — |
| dueBusinessDate | string / date | False | False | — |
| category | ExpenseCategoryCode | False | False | — |
| descriptionAr | string | False | True | — |
| allocations | PracticeAllocationPercentage[] | False | True | — |
| reason | string | False | True | — |
| descriptionEn | string | False | True | — |
| paidNow | boolean | False | False | — |
| payment | OperatingSettlementRequest | False | False | — |

## OperatingFinanceOptions

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| practices | ReportOption[] | False | True | — |
| receptions | ReportOption[] | False | True | — |
| categories | ReportOption[] | False | True | — |
| paymentMethods | ReportOption[] | False | True | — |
| cadences | ReportOption[] | False | True | — |
| amountPolicies | ReportOption[] | False | True | — |
| currencyCode | string | False | True | — |
| noProration | boolean | False | False | — |
| defaultReportTimeZoneId | string | False | True | — |
| pagination | ReportOptionsPagination | False | False | — |

## OperatingFinanceWriteResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| id | string / uuid | False | False | — |
| resourceType | string | False | True | — |
| detailRoute | string | False | True | — |
| rowVersion | string | False | True | — |
| obligationId | string / uuid | False | True | — |
| settlementId | string / uuid | False | True | — |

## OperatingHistoryResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| id | string / uuid | False | False | — |
| resourceType | string | False | True | — |
| resourceId | string / uuid | False | False | — |
| operation | string | False | True | — |
| reason | string | False | True | — |
| actorApplicationUserId | string / uuid | False | True | — |
| recordedAtUtc | string / date-time | False | False | — |
| snapshotRevisionId | string / uuid | False | True | — |
| planSnapshot | OperatingPlanRevisionResponse | False | False | — |
| obligationSnapshot | OperatingObligationRevisionResponse | False | False | — |

## OperatingHistoryResponseReportPage

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| items | OperatingHistoryResponse[] | False | True | — |
| totalCount | integer / int64 | False | False | — |
| pageNumber | integer / int32 | False | False | — |
| pageSize | integer / int32 | False | False | — |
| dataStatus | ReportDataStatus | False | False | — |
| warnings | string[] | False | True | — |

## OperatingMovementResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| id | string / uuid | False | False | — |
| obligationId | string / uuid | False | False | — |
| movementType | string | False | True | — |
| amount | number / double | False | False | — |
| businessDate | string / date | False | False | — |
| recordedAtUtc | string / date-time | False | False | — |
| originalSettlementId | string / uuid | False | True | — |

## OperatingMovementResponseReportPage

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| items | OperatingMovementResponse[] | False | True | — |
| totalCount | integer / int64 | False | False | — |
| pageNumber | integer / int32 | False | False | — |
| pageSize | integer / int32 | False | False | — |
| dataStatus | ReportDataStatus | False | False | — |
| warnings | string[] | False | True | — |

## OperatingObligationKind

Allowed values: `Salary | OneTimeExpense | RecurringExpense`.

## OperatingObligationResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| id | string / uuid | False | False | — |
| doctorId | string / uuid | False | False | — |
| planId | string / uuid | False | True | — |
| receptionId | string / uuid | False | True | — |
| kind | OperatingObligationKind | False | False | — |
| occurrenceKey | string | False | True | — |
| periodYear | integer / int32 | False | True | — |
| periodMonth | integer / int32 | False | True | — |
| businessDate | string / date | False | False | — |
| dueBusinessDate | string / date | False | False | — |
| plannedAmount | number / double | False | False | — |
| amount | number / double | False | False | — |
| currencyCode | string | False | True | — |
| amountPolicy | ExpenseAmountPolicy | False | False | — |
| actualAmountConfirmed | boolean | False | False | — |
| category | ExpenseCategoryCode | False | False | — |
| descriptionAr | string | False | True | — |
| descriptionEn | string | False | True | — |
| status | OperatingObligationStatus | False | False | — |
| dueState | string | False | True | — |
| activeSettlementId | string / uuid | False | True | — |
| allocations | OperatingAllocationResponse[] | False | True | — |
| recordedAtUtc | string / date-time | False | False | — |
| rowVersion | string | False | True | — |
| availableActions | ReportAction[] | False | True | — |

## OperatingObligationResponseReportPage

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| items | OperatingObligationResponse[] | False | True | — |
| totalCount | integer / int64 | False | False | — |
| pageNumber | integer / int32 | False | False | — |
| pageSize | integer / int32 | False | False | — |
| dataStatus | ReportDataStatus | False | False | — |
| warnings | string[] | False | True | — |

## OperatingObligationRevisionResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| id | string / uuid | False | False | — |
| revisionNumber | integer / int32 | False | False | — |
| amount | number / double | False | False | — |
| dueBusinessDate | string / date | False | False | — |
| category | ExpenseCategoryCode | False | False | — |
| descriptionAr | string | False | True | — |
| descriptionEn | string | False | True | — |
| actualAmountConfirmed | boolean | False | False | — |
| status | OperatingObligationStatus | False | False | — |
| reason | string | False | True | — |
| actorApplicationUserId | string / uuid | False | False | — |
| recordedAtUtc | string / date-time | False | False | — |
| allocations | OperatingAllocationResponse[] | False | True | — |

## OperatingObligationStatus

Allowed values: `Unpaid | Paid | Cancelled`.

## OperatingPaymentMethod

Allowed values: `Cash | Card | Wallet | BankTransfer`.

## OperatingPlanKind

Allowed values: `Salary | RecurringExpense`.

## OperatingPlanRequest

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| amount | number / double | False | False | — |
| dueDay | integer / int32 | False | False | — |
| effectiveFrom | string / date | False | False | — |
| effectiveTo | string / date | False | True | — |
| allocations | PracticeAllocationPercentage[] | False | True | — |
| descriptionAr | string | False | True | — |
| descriptionEn | string | False | True | — |
| receptionId | string / uuid | False | True | — |
| category | ExpenseCategoryCode | False | False | — |
| cadence | ExpenseCadence | False | False | — |
| amountPolicy | ExpenseAmountPolicy | False | False | — |
| rowVersion | string | False | True | — |
| reason | string | False | True | — |

## OperatingPlanResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| id | string / uuid | False | False | — |
| doctorId | string / uuid | False | False | — |
| receptionId | string / uuid | False | True | — |
| kind | OperatingPlanKind | False | False | — |
| status | OperatingPlanStatus | False | False | — |
| firstCoveredMonth | string / date | False | False | — |
| noProration | boolean | False | False | — |
| effectiveFrom | string / date | False | False | — |
| effectiveTo | string / date | False | True | — |
| revisions | OperatingPlanRevisionResponse[] | False | True | — |
| rowVersion | string | False | True | — |
| availableActions | ReportAction[] | False | True | — |

## OperatingPlanResponseReportPage

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| items | OperatingPlanResponse[] | False | True | — |
| totalCount | integer / int64 | False | False | — |
| pageNumber | integer / int32 | False | False | — |
| pageSize | integer / int32 | False | False | — |
| dataStatus | ReportDataStatus | False | False | — |
| warnings | string[] | False | True | — |

## OperatingPlanRevisionResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| id | string / uuid | False | False | — |
| revisionNumber | integer / int32 | False | False | — |
| amount | number / double | False | False | — |
| currencyCode | string | False | True | — |
| dueDay | integer / int32 | False | False | — |
| effectiveFrom | string / date | False | False | — |
| effectiveTo | string / date | False | True | — |
| cadence | ExpenseCadence | False | False | — |
| amountPolicy | ExpenseAmountPolicy | False | False | — |
| category | ExpenseCategoryCode | False | False | — |
| descriptionAr | string | False | True | — |
| descriptionEn | string | False | True | — |
| status | OperatingPlanStatus | False | False | — |
| isLifecycleChange | boolean | False | False | — |
| allocations | OperatingAllocationResponse[] | False | True | — |
| recordedAtUtc | string / date-time | False | False | — |
| actorApplicationUserId | string / uuid | False | False | — |
| reason | string | False | True | — |

## OperatingPlanStatus

Allowed values: `Active | Paused | Ended`.

## OperatingReversalRequest

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| businessDate | string / date | False | False | — |
| rowVersion | string | False | True | — |
| reason | string | False | True | — |
| reportTimeZoneId | string | False | True | — |

## OperatingSettlementRequest

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| amount | number / double | False | False | — |
| paidBusinessDate | string / date | False | False | — |
| paymentMethod | OperatingPaymentMethod | False | False | — |
| rowVersion | string | False | True | — |
| reference | string | False | True | — |
| backdateReason | string | False | True | — |
| reportTimeZoneId | string | False | True | — |

## OperatingSettlementResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| id | string / uuid | False | False | — |
| obligationId | string / uuid | False | False | — |
| obligationRevisionId | string / uuid | False | False | — |
| receiptNumber | string | False | True | — |
| amount | number / double | False | False | — |
| currencyCode | string | False | True | — |
| paymentMethod | OperatingPaymentMethod | False | False | — |
| reference | string | False | True | — |
| businessDate | string / date | False | False | — |
| reportTimeZoneId | string | False | True | — |
| backdateReason | string | False | True | — |
| actorApplicationUserId | string / uuid | False | False | — |
| recordedAtUtc | string / date-time | False | False | — |
| isReversed | boolean | False | False | — |
| reversalId | string / uuid | False | True | — |
| obligationRowVersion | string | False | True | — |
| descriptionAr | string | False | True | — |
| descriptionEn | string | False | True | — |
| allocations | OperatingAllocationResponse[] | False | True | — |
| availableActions | ReportAction[] | False | True | — |

## OperatingTransitionRequest

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| rowVersion | string | False | True | — |
| reason | string | False | True | — |

## OutboxHealth

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| pending | integer / int64 | False | False | — |
| processing | integer / int64 | False | False | — |
| sent | integer / int64 | False | False | — |
| failed | integer / int64 | False | False | — |
| oldestPendingAtUtc | string / date-time | False | True | — |
| expiredClaims | integer / int64 | False | False | — |
| dateBasis | string | False | True | — |

## PatientCapability

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| code | string | False | True | — |
| nameAr | string | False | True | — |
| nameEn | string | False | True | — |
| available | boolean | False | False | — |
| route | string | False | True | — |
| unavailableReasonCode | string | False | True | — |

## PatientDashboardCapabilities

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| patientId | string / uuid | False | False | — |
| generatedAtUtc | string / date-time | False | False | — |
| capabilities | PatientCapability[] | False | True | — |
| crossDoctorSharingEnabled | boolean | False | False | — |
| guardianSharingEnabled | boolean | False | False | — |
| warnings | string[] | False | True | — |

## PatientDashboardOverview

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| patientId | string / uuid | False | False | — |
| generatedAtUtc | string / date-time | False | False | — |
| profile | PatientProfileResponse | False | False | — |
| upcomingBookings | ReservationPagedResponse | False | False | — |
| activeTickets | MyActiveTicketResponse[] | False | True | — |
| medicalSummary | RecordSummary | False | False | — |
| recentPermittedTimeline | TimelineEntryRecordPage | False | False | — |
| followUpAvailability | FollowUpEligibilityResponseClinicalPage | False | False | — |
| recentFinancialTransactions | PatientFinancialTransactionItemFinancialPage | False | False | — |
| family | MyFamilyResponse | False | False | — |
| capabilities | PatientDashboardCapabilities | False | False | — |
| warnings | string[] | False | True | — |

## PatientFinancialTransactionItem

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| transactionType | FinancialTransactionType | False | False | — |
| transactionId | string / uuid | False | False | — |
| transactionNumber | string | False | True | — |
| originalPaymentTransactionNumber | string | False | True | — |
| doctorId | string / uuid | False | False | — |
| doctorNameAr | string | False | True | — |
| doctorNameEn | string | False | True | — |
| doctorPracticeId | string / uuid | False | False | — |
| practiceNameAr | string | False | True | — |
| practiceNameEn | string | False | True | — |
| ticketNumber | integer / int32 | False | False | — |
| amount | number / double | False | False | — |
| currencyCode | string | False | True | — |
| method | PaymentMethod | False | False | — |
| referenceNumber | string | False | True | — |
| occurredOnUtc | string / date-time | False | False | — |
| businessDate | string / date | False | False | — |
| isRefunded | boolean | False | False | — |
| refundReasonCode | RefundReasonCode | False | False | — |
| refundReason | string | False | True | — |

## PatientFinancialTransactionItemFinancialPage

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| items | PatientFinancialTransactionItem[] | False | True | — |
| totalCount | integer / int32 | False | False | — |
| pageNumber | integer / int32 | False | False | — |
| pageSize | integer / int32 | False | False | — |

## PatientProfileResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| patientId | string / uuid | False | False | — |
| nameAr | string | False | True | — |
| nameEn | string | False | True | — |
| dateOfBirth | string / date | False | False | — |
| gender | Gender | False | False | — |
| phoneNumber | string | False | True | — |
| email | string | False | True | — |
| hasProfileImage | boolean | False | False | — |
| rowVersion | string | False | True | — |

## PaymentMethod

Allowed values: `LegacyUnspecified | Cash | Card | Wallet`.

## PlatformAttentionItem

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| id | string / uuid | False | False | — |
| resourceType | string | False | True | — |
| titleAr | string | False | True | — |
| titleEn | string | False | True | — |
| status | string | False | True | — |
| recordedAtUtc | string / date-time | False | False | — |
| detailRoute | string | False | True | — |
| availableActions | ReportAction[] | False | True | — |

## PlatformAttentionItemReportPage

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| items | PlatformAttentionItem[] | False | True | — |
| totalCount | integer / int64 | False | False | — |
| pageNumber | integer / int32 | False | False | — |
| pageSize | integer / int32 | False | False | — |
| dataStatus | ReportDataStatus | False | False | — |
| warnings | string[] | False | True | — |

## PlatformAttentionResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| generatedAtUtc | string / date-time | False | False | — |
| pendingDoctorCount | integer / int64 | False | False | — |
| pendingSpecializationCount | integer / int64 | False | False | — |
| openInvestigationCount | integer / int64 | False | True | — |
| failedOutboxCount | integer / int64 | False | True | — |
| items | PlatformAttentionItemReportPage | False | False | — |
| warnings | string[] | False | True | — |

## PlatformDataQualityResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| generatedAtUtc | string / date-time | False | False | — |
| items | DataQualityItem[] | False | True | — |
| warnings | string[] | False | True | — |

## PlatformJobHealthResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| generatedAtUtc | string / date-time | False | False | — |
| generationHealth | string | False | True | — |
| generationEnabled | boolean | False | False | — |
| pollingIntervalSeconds | integer / int32 | False | False | — |
| recentRuns | JobRunMetadata[] | False | True | — |
| outbox | OutboxHealth | False | False | — |
| warnings | string[] | False | True | — |
| reportingPolicyRefreshHealth | string | False | True | — |
| reportingPolicyRefreshRuns | JobRunMetadata[] | False | True | — |

## PracticeAllocationPercentage

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| practiceId | string / uuid | False | False | — |
| percentage | number / double | False | False | — |

## ProblemDetails

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| type | string | False | True | — |
| title | string | False | True | — |
| status | integer / int32 | False | True | — |
| detail | string | False | True | — |
| instance | string | False | True | — |

## ReceptionAttentionResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| asOfUtc | string / date-time | False | False | — |
| metrics | ReportMetric[] | False | True | — |
| worklist | WorklistItemReportPage | False | False | — |
| warnings | string[] | False | True | — |

## RecordSummary

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| completedVisitCount | integer / int32 | False | False | — |
| currentPrescriptionCount | integer / int32 | False | False | — |
| officialLabResultCount | integer / int32 | False | False | — |
| officialRadiologyResultCount | integer / int32 | False | False | — |
| hiddenEncounterCount | integer / int32 | False | False | — |

## RefundReasonCode

Allowed values: `PatientRequestedCancellation | DoctorUnavailable | DuplicatePayment | WrongPaymentMethod | OperationalError | Other`.

## ReportAction

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| code | string | False | True | — |
| allowed | boolean | False | False | — |
| route | string | False | True | — |
| unavailableReasonCode | string | False | True | — |

## ReportActor

Allowed values: `Doctor | Reception | SuperAdmin | Patient`.

## ReportCatalogEntry

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| reportCode | string | False | True | — |
| titleAr | string | False | True | — |
| titleEn | string | False | True | — |
| description | string | False | True | — |
| allowedFilters | string[] | False | True | — |
| availableGroupings | ReportGrouping[] | False | True | — |
| scope | string | False | True | — |
| canPreview | boolean | False | False | — |
| canDrillDown | boolean | False | False | — |
| canExportPdf | boolean | False | False | — |
| canExportExcel | boolean | False | False | — |
| exportLimits | ExportLimits | False | False | — |
| requiredPermissions | string[] | False | True | — |
| dataStatus | ReportDataStatus | False | False | — |
| exportRequiredPermissions | string[] | False | True | — |
| minimumCalendarDays | integer / int32 | False | False | — |
| maximumCalendarDays | integer / int32 | False | False | — |
| dataAvailability | string | False | True | — |
| previewRoute | string | False | True | — |
| drilldownRoute | string | False | True | — |
| exportRoute | string | False | True | — |

## ReportComparison

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| metricCode | string | False | True | — |
| previousFromDate | string / date | False | False | — |
| previousToDate | string / date | False | False | — |
| currentValue | number / double | False | True | — |
| previousValue | number / double | False | True | — |
| absoluteChange | number / double | False | True | — |
| relativeChangePercent | number / double | False | True | — |
| dataStatus | ReportDataStatus | False | False | — |
| warningCode | string | False | True | — |

## ReportDataStatus

Allowed values: `Complete | Partial | NotAvailable | NotMeasured | NotInstrumented | Stale`.

## ReportFilter

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| fromDate | string / date | False | True | — |
| toDate | string / date | False | True | — |
| practiceId | string / uuid | False | True | — |
| doctorId | string / uuid | False | True | — |
| segmentId | string / uuid | False | True | — |
| visitTypeId | string / uuid | False | True | — |
| bookingSource | ReservationBookingSource | False | False | — |
| reservationStatus | ReservationStatus | False | False | — |
| ticketStatus | TicketStatus | False | False | — |
| asOfUtc | string / date-time | False | True | — |
| reportTimeZoneId | string | False | True | — |
| grouping | ReportGrouping | False | False | — |
| comparePreviousPeriod | boolean | False | False | — |
| pageNumber | integer / int32 | False | False | — |
| pageSize | integer / int32 | False | False | — |
| sortBy | string | False | True | — |
| sortDirection | string | False | True | — |
| expenseCategory | ExpenseCategoryCode | False | False | — |
| receptionId | string / uuid | False | True | — |
| planId | string / uuid | False | True | — |

## ReportFilterOptions

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| practices | ReportOption[] | False | True | — |
| segments | ReportOption[] | False | True | — |
| visitTypes | ReportOption[] | False | True | — |
| bookingSources | ReportOption[] | False | True | — |
| reservationStatuses | ReportOption[] | False | True | — |
| ticketStatuses | ReportOption[] | False | True | — |
| groupings | ReportGrouping[] | False | True | — |
| defaultReportTimeZoneId | string | False | True | — |
| exportLimits | ExportLimits | False | False | — |
| pagination | ReportOptionsPagination | False | False | — |

## ReportGrouping

Allowed values: `Day | Week | Month | Practice | Segment | VisitType | Source | Status`.

## ReportMetric

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| metricCode | string | False | True | — |
| nameAr | string | False | True | — |
| nameEn | string | False | True | — |
| value | number / double | False | True | — |
| unit | string | False | True | — |
| numerator | number / double | False | True | — |
| denominator | number / double | False | True | — |
| eligibleSampleCount | integer / int64 | False | False | — |
| excludedSampleCount | integer / int64 | False | False | — |
| dataStatus | ReportDataStatus | False | False | — |
| warningCode | string | False | True | — |
| availableActions | ReportAction[] | False | True | — |

## ReportModel

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| reportCode | string | False | True | — |
| titleAr | string | False | True | — |
| titleEn | string | False | True | — |
| description | string | False | True | — |
| appliedFilters | ReportFilter | False | False | — |
| scope | ReportScope | False | False | — |
| window | ReportingWindow | False | False | — |
| dataFreshnessUtc | string / date-time | False | False | — |
| dataStatus | ReportDataStatus | False | False | — |
| metrics | ReportMetric[] | False | True | — |
| series | ReportSeriesPoint[] | False | True | — |
| breakdowns | ReportSeriesPoint[] | False | True | — |
| availableActions | ReportAction[] | False | True | — |
| warnings | string[] | False | True | — |
| eligibleSampleCount | integer / int64 | False | False | — |
| excludedSampleCount | integer / int64 | False | False | — |
| records | ReportRecord[] | False | True | — |
| detailRowCount | integer / int64 | False | False | — |
| previousPeriodComparisons | ReportComparison[] | False | True | — |

## ReportOption

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| code | string | False | True | — |
| nameAr | string | False | True | — |
| nameEn | string | False | True | — |
| id | string / uuid | False | True | — |

## ReportOptionsPagination

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| pageNumber | integer / int32 | False | False | — |
| pageSize | integer / int32 | False | False | — |
| practiceTotalCount | integer / int64 | False | False | — |
| segmentTotalCount | integer / int64 | False | False | — |
| visitTypeTotalCount | integer / int64 | False | False | — |
| receptionTotalCount | integer / int64 | False | False | — |

## ReportRecord

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| id | string / uuid | False | False | — |
| resourceType | string | False | True | — |
| practiceId | string / uuid | False | True | — |
| businessDate | string / date | False | False | — |
| recordedAtUtc | string / date-time | False | False | — |
| reference | string | False | True | — |
| status | string | False | True | — |
| patientId | string / uuid | False | True | — |
| amount | number / double | False | True | — |
| detailRoute | string | False | True | — |
| availableActions | ReportAction[] | False | True | — |
| metricValue | number / double | False | True | — |
| metricDenominator | number / double | False | True | — |
| exclusionReasonCode | string | False | True | — |
| sourceRecordId | string / uuid | False | True | — |
| evidenceSourceIds | string / uuid[] | False | True | — |
| recordedByApplicationUserId | string / uuid | False | True | — |

## ReportRecordReportPage

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| items | ReportRecord[] | False | True | — |
| totalCount | integer / int64 | False | False | — |
| pageNumber | integer / int32 | False | False | — |
| pageSize | integer / int32 | False | False | — |
| dataStatus | ReportDataStatus | False | False | — |
| warnings | string[] | False | True | — |

## ReportScope

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| actor | ReportActor | False | False | — |
| actorId | string / uuid | False | False | — |
| doctorId | string / uuid | False | True | — |
| patientId | string / uuid | False | True | — |
| practiceIds | string / uuid[] | False | True | — |
| permissions | string[] | False | True | — |

## ReportSeriesPoint

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| key | string | False | True | — |
| nameAr | string | False | True | — |
| nameEn | string | False | True | — |
| metrics | ReportMetric[] | False | True | — |

## ReportingWindow

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| fromDate | string / date | False | False | — |
| toDate | string / date | False | False | — |
| asOfUtc | string / date-time | False | False | — |
| generatedAtUtc | string / date-time | False | False | — |
| reportTimeZoneId | string | False | True | — |

## ReservationBookingSource

Allowed values: `Patient | FamilyMember | Reception`.

## ReservationListItemResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| reservationId | string / uuid | False | False | — |
| reservationReference | string | False | True | — |
| status | string | False | True | — |
| isLate | boolean | False | False | — |
| patientId | string / uuid | False | False | — |
| patientNameAr | string | False | True | — |
| patientNameEn | string | False | True | — |
| doctorPracticeId | string / uuid | False | False | — |
| practiceNameAr | string | False | True | — |
| practiceNameEn | string | False | True | — |
| businessDate | string / date | False | False | — |
| localTime | string / time | False | False | — |
| segmentNameAr | string | False | True | — |
| segmentNameEn | string | False | True | — |
| bookingSource | string | False | True | — |
| price | number / double | False | False | — |
| rowVersion | string | False | True | — |

## ReservationListSummary

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| total | integer / int64 | False | False | — |
| active | integer / int64 | False | False | — |
| late | integer / int64 | False | False | — |
| noShow | integer / int64 | False | False | — |
| cancelled | integer / int64 | False | False | — |
| convertedToTicket | integer / int64 | False | False | — |
| expired | integer / int64 | False | False | — |

## ReservationPagedResponse

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| items | ReservationListItemResponse[] | False | True | — |
| totalCount | integer / int64 | False | False | — |
| pageNumber | integer / int32 | False | False | — |
| pageSize | integer / int32 | False | False | — |
| summary | ReservationListSummary | False | False | — |

## ReservationStatus

Allowed values: `Active | Cancelled | NoShow | Expired | ConvertedToTicket`.

## TicketStatus

Allowed values: `Waiting | Called | InProgress | NoShow | Completed | Cancelled`.

## TimelineCapabilities

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| canViewDetails | boolean | False | False | — |
| canViewAttachments | boolean | False | False | — |

## TimelineEntry

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| type | string | False | True | — |
| entryId | string | False | True | — |

## TimelineEntryRecordPage

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| items | TimelineEntry[] | False | True | — |
| nextCursor | string | False | True | — |
| hasMore | boolean | False | False | — |

## WorklistCategory

Allowed values: `UpcomingBooking | AwaitingCheckIn | LateArrival | QueueAttention | ReservationNoShow | TicketNoShow | OpenHandover`.

## WorklistItem

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| id | string / uuid | False | False | — |
| resourceType | string | False | True | — |
| category | WorklistCategory | False | False | — |
| categoryAr | string | False | True | — |
| categoryEn | string | False | True | — |
| businessDate | string / date | False | False | — |
| occurredAtUtc | string / date-time | False | False | — |
| reference | string | False | True | — |
| status | string | False | True | — |
| patientId | string / uuid | False | True | — |
| patientNameAr | string | False | True | — |
| patientNameEn | string | False | True | — |
| availableActions | ReportAction[] | False | True | — |

## WorklistItemReportPage

| Field | Type | Required | Nullable | Constraints/default |
|---|---|---|---|---|
| items | WorklistItem[] | False | True | — |
| totalCount | integer / int64 | False | False | — |
| pageNumber | integer / int32 | False | False | — |
| pageSize | integer / int32 | False | False | — |
| dataStatus | ReportDataStatus | False | False | — |
| warnings | string[] | False | True | — |

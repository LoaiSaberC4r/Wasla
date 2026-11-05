> Historical v1.0 reference. Commercial v1.2 supersedes conflicting behavior below. Implement clients from the [v1.2 API and Angular handoff](V1.2-API-ANGULAR-HANDOFF.md). See the [approved source](../../SOURCE-OF-TRUTH-SUBSCRIPTION-COMMERCIAL-ADDENDUM.md) and [impact plan](V1.2-IMPACT-PLAN.md).

# Commercial subscription API — Angular handoff

Version 1.0. All routes below use `/api/v1`. Authentication, permission errors, validation, localization, and ProblemDetails follow the existing Wasla API. JSON enums use their names. UTC timestamps use ISO 8601 with `Z`; period ends are exclusive. Row versions are opaque Base64 strings.

## Registration

The existing `POST /auth/doctors/register` remains **multipart/form-data** because it uploads verification documents. Keep the existing account/profile/document fields and add these nested form field names:

| Field | Paid Full Platform | Starter trial | Booking Only |
|---|---|---|---|
| `Subscription.ProductCode` | `FULL_PLATFORM` | `FULL_PLATFORM` | `ONLINE_BOOKING_ONLY` |
| `Subscription.PlanCode` | `STARTER`, `PROFESSIONAL`, `PREMIUM` | `STARTER` | `BOOKING_ONLY` |
| `Subscription.BillingCycle` | `Monthly` or `Annual` | omit | `Monthly` |
| `Subscription.OfferCode` | omit | `STARTER_FREE_TRIAL_20D` | omit |
| `Subscription.SelectedPracticeCount` | omit | omit | configured positive count |

The logical selection JSON, also used by subscription change requests, is:

```json
{
  "productCode": "FULL_PLATFORM",
  "planCode": "STARTER",
  "billingCycle": "Monthly",
  "offerCode": null,
  "selectedPracticeCount": null
}
```

Do not send price, currency, limits, or features. Unknown JSON selection members are rejected; unrecognized form fields never supply commercial terms. Booking Only is disabled until configured. A missing selection is invalid.

Successful registration returns HTTP 201:

```json
{
  "doctorId": "GUID",
  "applicationUserId": "GUID",
  "approvalStatus": "Pending",
  "subscriptionId": "GUID",
  "subscription": {
    "productCode": "FULL_PLATFORM",
    "planCode": "STARTER",
    "billingCycle": null,
    "offerCode": "STARTER_FREE_TRIAL_20D",
    "selectedPracticeCount": null
  },
  "subscriptionStatus": "Pending"
}
```

Trial activation happens through the existing doctor-approval endpoint. There is no start-trial route. Paid subscriptions remain Pending after approval.

## Public catalog

`GET /public/commercial/catalog` is anonymous. It returns:

- `catalogVersion`, `currency`.
- `products[]`: `code`, Arabic/English names, `enabled`, and `plans[]`.
- Plan fields: `code`, names, `pricing`, `billingCycles[]`, and `features[]`.
- Full Platform `pricing`: `code`, `monthlyPrice`, `annualPrice`, `practiceLimit`, `receptionLimit`.
- Feature fields: `code`, names, `groupCode`, `implementationStatus`, `releaseReadiness`, `accessLevel`, `isReleaseReady`.
- `trialOffer`: enabled state, offer code, duration, approval-start rule, limits, reminder offsets, and extension policy.
- `bookingOnly`: enabled state, plan code, `maximumSelectablePractices`, `pricingTiers[]` with inclusive min/max and **total** monthly price.
- `paymentMethods`: `cash`, `card`, `eWallet` booleans.
- `policies`: automatic-renewal/proration/fee/commission/tax flags and manual-renewal/cancellation policy.

Booking Only plan `pricing` is null; use configured tiers and the selected count. A disabled product must not be selectable. Internal transition plans are omitted. Display included features' readiness metadata; inclusion does not imply a verified release-ready feature.

## Doctor subscription

| Method | Route | Permission | Body |
|---|---|---|---|
| GET | `/doctors/me/subscription` | `Subscriptions.ViewOwn` | none |
| POST | `/doctors/me/subscription/selection` | `Subscriptions.RequestChangeOwn` | selection object directly |
| POST | `/doctors/me/subscription/change-requests` | `Subscriptions.RequestChangeOwn` | change request below |
| POST | `/doctors/me/subscription/cancel-at-period-end` | `Subscriptions.CancelAtPeriodEndOwn` | `{ "rowVersion": "BASE64" }` |

The selection endpoint records a pending paid choice for an unpaid/ended trial or a legacy doctor. It never starts a trial or activates payment. An internal legacy transition may be replaced by the doctor's first paid product choice. Selected commercial subscriptions cannot switch products through change or renewal operations.

Doctor, admin-detail, and successful mutation responses use this envelope:

```json
{
  "subscription": {
    "id": "GUID",
    "doctorId": "GUID",
    "productId": "GUID",
    "planId": "GUID",
    "subscriptionKind": "Paid",
    "status": "Active",
    "billingCycle": "Monthly",
    "selectedPracticeCount": null,
    "priceAmount": 499,
    "currency": "EGP",
    "practiceLimit": 1,
    "receptionLimit": 1,
    "startsAtUtc": "2026-11-02T12:00:00Z",
    "endsAtUtc": "2026-12-02T12:00:00Z",
    "trialStartedAtUtc": null,
    "trialEndsAtUtc": null,
    "trialExtensionCount": 0,
    "catalogVersion": "1.0.0",
    "willNotRenew": false,
    "cancellationRequestedOnUtc": null,
    "activatedByApplicationUserId": "GUID",
    "createdOnUtc": "2026-11-02T10:00:00Z",
    "modifiedOnUtc": "2026-11-02T12:00:00Z",
    "rowVersion": "BASE64",
    "termsSnapshotJson": "IMMUTABLE AUDIT JSON",
    "terms": {
      "productId": "GUID",
      "productCode": "FULL_PLATFORM",
      "planId": "GUID",
      "planCode": "STARTER",
      "kind": "Paid",
      "billingCycle": "Monthly",
      "selectedPracticeCount": null,
      "priceAmount": 499,
      "currency": "EGP",
      "practiceLimit": 1,
      "receptionLimit": 1,
      "catalogVersion": "1.0.0",
      "durationDays": null,
      "reminderDaysBeforeEnd": [],
      "features": [{ "featureId": "GUID", "code": "DOCTOR_PROFILE", "accessLevel": "Full" }]
    }
  },
  "usage": { "practiceCount": 1, "receptionCount": 1 },
  "entitlements": [{ "id": "GUID", "subscriptionId": "GUID", "featureId": "GUID", "featureCodeSnapshot": "DOCTOR_PROFILE", "accessLevel": "Full", "createdOnUtc": "2026-11-02T10:00:00Z" }],
  "changeRequests": [],
  "payments": null,
  "history": null,
  "rowVersion": "BASE64",
  "pendingSubscription": null,
  "nextPeriod": null,
  "doctor": { "doctorId": "GUID", "nameAr": "الطبيب", "nameEn": "Doctor", "email": "doctor@example.test", "approvalStatus": "Approved" }
}
```

The example shortens feature arrays, not the underlying accepted matrix. `pendingSubscription` and `nextPeriod` use the same subscription shape. Admin details include payments and history arrays; own-doctor responses leave those two arrays null. Reception has no access to subscription financial administration.

States: `Pending`, `Active`, `Expired`, `Cancelled`, `Superseded`, `Scheduled`. Scheduled means a confirmed, already-paid future period. Access is effective only inside its start/end window. The worker updates lifecycle status; the client must not implement renewal or charges.

Approved flat change request body (row version is optional for a new change request):

```json
{
  "changeType": "UpgradePlan",
  "targetPlanCode": "PROFESSIONAL",
  "targetPracticeCount": null,
  "targetBillingCycle": null,
  "reason": "Need more clinic capacity"
}
```

For Booking Only, use `IncreaseBookingPracticeLimit`, `targetPlanCode: "BOOKING_ONLY"`, and the desired `targetPracticeCount`. Null target cycle preserves the current cycle. Other accepted input names are `DowngradePlan`, `DecreaseBookingPracticeLimit`, and `ChangeBillingCycle`. The compact names `Upgrade`, `Downgrade`, and `BillingCycle` are also accepted, along with a nested `selection` object instead of flat target fields. An optional supplied row version must match. Product always comes from the current subscription for flat requests.

Returned `changeType` categories are `Upgrade`, `Downgrade`, or `BillingCycle`. Requests include `id`, `subscriptionId`, requester/reviewer IDs and timestamps, target plan/count/cycle, `status`, rejection reason, `targetPriceSnapshot`, server-calculated `differenceAmount`, `effectiveAtUtc`, accepted target terms, and `rowVersion`. Review states are `Pending`, `Applied`, `Scheduled`, or `Rejected` in the implemented workflows; enum values also reserve `Approved` and `Cancelled`.

Only higher same-cycle Full Platform plans and Booking Only count increases are immediate upgrades. Downgrades and cycle changes take effect at renewal. No client amount is used to calculate the difference.

## SuperAdmin operations

| Method | Route under `/admin/subscriptions` | Permission |
|---|---|---|
| GET | `/` | `Subscriptions.ViewAll` |
| GET | `/{subscriptionId}` | `Subscriptions.ViewDetails` |
| POST | `/{subscriptionId}/activate` | `Subscriptions.Activate` |
| POST | `/{subscriptionId}/trial-extend` | `Subscriptions.ExtendTrial` |
| POST | `/{subscriptionId}/change-requests/{requestId}/approve` | `Subscriptions.ReviewChangeRequests` |
| POST | `/{subscriptionId}/change-requests/{requestId}/reject` | `Subscriptions.ReviewChangeRequests` |
| POST | `/{subscriptionId}/renew` | `Subscriptions.Renew` |
| POST | `/legacy-transition/enroll` | `Subscriptions.EnrollLegacyTransition` |

The list accepts `pageNumber` (default 1), `pageSize` (default 20, maximum 100), `search`, `productCode`, `planCode`, `status`, `createdFromUtc`, `createdToUtc`, `endsFromUtc`, and `endsToUtc`. Upper date bounds are exclusive. It returns `{ items, totalCount, pageNumber, pageSize }`, with subscription objects in `items`.

Activation and renewal body:

```json
{
  "rowVersion": "BASE64",
  "paymentMethod": "Cash",
  "amountReceived": 499,
  "paidAtUtc": null,
  "reference": null,
  "notes": null
}
```

Send an `Idempotency-Key` header, at most 150 characters, for activation, renewal, and approval. Reuse the same key only for a retry of the same payment intent. A fingerprint covers actor, operation, subscription, change request, amount, method, payment time, notes, reference, and selection. Changed payment intent with an old key returns a conflict. Row versions may be refreshed without changing the intent.

Activation requires an Approved doctor and Pending paid selection. The amount must exactly equal the stored expected amount. Card and EWallet are rejected while disabled. There are no partial payments or discounts. `paidAtUtc`, if supplied, must be UTC and not in the future.

Renewal may add `targetPlanCode`, `targetPracticeCount`, and `targetBillingCycle`, or a nested `selection`, to choose the renewal terms; product cannot change. An approved scheduled change takes precedence over a new body selection. Renewal uses current configuration and revalidates reduced capacities. Before expiry, the new period starts at the old end; after expiry, it starts at activation time.

Trial extension body: `{ "rowVersion": "BASE64", "additionalDays": 5, "reason": "Approved commercial exception" }`. Only active trials may be extended. Old/new dates and the actor are audited.

Upgrade approval adds `changeRowVersion` and the exact server-returned difference as `amountReceived`; `rowVersion` still refers to the subscription. Downgrade/cycle approval uses both row versions, with no payment. Rejection uses both row versions and a mandatory `reason`.

The enrollment route takes no business body. It processes at most the configured batch size. Its response includes batch `id`, `startedAtUtc`, `endsAtUtc`, `durationDays`, actor, status (`Running` or `Completed`), completion time, creation time, and row version. Repeat while Running; Completed replays are unchanged. Never call this automatically from Angular startup.

## Practice presentation and reception account state

`PUT /doctors/me/practices/{practiceId}/booking-presentation/price`, permission `DoctorPractices.ManageOwn`:

```json
{ "newConsultationPrice": 250, "rowVersion": null }
```

This reuses the default segment/new-consultation price. Use null row version on first creation, then its returned row version on edits. Response: existing `DoctorPracticePriceResponse` (`id`, `segmentId`, `visitTypeId`, `price`, `rowVersion`). Booking Only may use this narrow route, branding, schedule, and non-operational practice configuration; it cannot enable walk-in or change operational ticket/check-in settings.

`PUT /doctors/me/receptions/{receptionId}/account-state`, permission `ReceptionUsers.ManageAccountStateOwn`:

```json
{ "isActive": false, "rowVersion": "RECEPTION_BASE64" }
```

Returns the existing Reception response plus `isActive` and the new reception row version. Assignment activation remains a separate operation. Reactivation requires an available seat. Deactivation preserves the account, profile, and assignment records.

## Error handling and menu behavior

Use actual returned `entitlements`, usage, and pending/future periods for commercial UI. Existing role/permission/scope checks still apply. Hide unavailable operational entry points, but rely on backend authorization for every request. Keep profile/onboarding/subscription screens accessible for pending payment. Do not hide patient medical history, consent, or family rights based on the doctor's plan.

| HTTP | Relevant codes/meaning |
|---|---|
| 400 | Malformed JSON, invalid enum binding, unsupported selection members |
| 401 | Existing authentication failure |
| 403 | `Subscription.FeatureNotEntitled`, `AccessLevelInsufficient`, `NotActive`, `Pending`, `Expired`, `DoctorNotApproved`, or existing scope/permission denial |
| 404 | `Subscription.NotFound`, or existing opaque resource-not-found behavior |
| 409 | `Subscription.ConcurrencyConflict`, `PracticeLimitReached`, `ReceptionLimitReached`, `PaymentAlreadyRecorded`, `ChangeRequestAlreadyPending`, `DowngradeUsageExceedsLimit` |
| 422 | `Subscription.InvalidSelection`, `ProductNotAvailable`, `PlanNotAvailable`, `InvalidBillingCycle`, `TrialNotEligible`, `TrialExtensionInvalid`, `PaymentMethodDisabled`, `PaymentAmountMismatch`, `PaymentRequired`, `InvalidTransition`, `ChangeRequestInvalid`, `ChangeRequestNotFound`, `BookingOnlyNotConfigured`, `ConfigurationInvalid`, or validation errors |

Capacity messages report actual usage and the limit. Downgrade excess errors also supply a JSON `details` string containing `CurrentPracticeCount`, `CurrentReceptionCount`, `TargetPracticeLimit`, and `TargetReceptionLimit`. Refresh details on concurrency conflicts. Never display database exception internals or infer that a rejected request charged money.

## PR #36 authorization clarification — 2026-11-03

Doctor and Reception archive permissions are commercial reads:

| Existing permission | Feature | Required access |
|---|---|---|
| `Archives.Tickets.ViewPractice` / `ViewAssigned` | `TICKET_QUEUE` | ReadOnly |
| `Archives.Reservations.ViewPractice` / `ViewAssigned` | `RESERVATION_MANAGEMENT` | ReadOnly |

The requested permission must pass its own commercial check as well as the existing account, role, ownership, assignment, and governance checks. Missing mappings and feature snapshots fail closed. Patient/Guardian archive rights and SuperAdmin metadata routes keep their existing authorization policies.

An effective current read entitlement permits the actor's authorized owned history. When access instead relies on past accepted entitlements, the original resource must fall inside a proven subscription period. Legacy transition history can cover older owned records, but ends at transition expiry or paid supersession. Archive collection filters run before pagination; `hasMore`, linked Ticket IDs, and cursors do not expose excluded records. A changed scope or historical entitlement invalidates a cursor. Use the existing error response to refresh a stale page.

For an otherwise valid active reservation, a Doctor whose accepted `RESERVATION_MANAGEMENT` access is ReadOnly receives:

```json
{
  "capabilities": {
    "canCancel": false,
    "cancelBlockedReason": "Subscription.AccessLevelInsufficient",
    "canReschedule": false,
    "rescheduleBlockedReason": "Subscription.AccessLevelInsufficient",
    "canRestoreFromNoShow": false
  },
  "linkedTicketId": null,
  "canViewLinkedTicket": false
}
```

The linked Ticket fields above apply when no independently authorized current or historical Ticket read exists. Flags derive from current stored permissions, Reception assignment grants, and accepted entitlements; a role name or linked identifier alone is insufficient. Missing role/assignment rights return an access-denial reason. Cancelled/invalid reservations retain lifecycle-specific reasons, and archived reservations always return false mutation flags with `Archive.ReadOnly`. Inactive-practice historical reads remain read-only with `Reservation.PracticeUnavailable` where that existing rule applies.

Doctor/Reception capabilities may retain a narrowly authorized action on an actual pre-expiry commitment when the existing continuity policy permits that same mutation endpoint. Expiry never authorizes a new booking or post-expiry queue commitment. Patient self-service capabilities continue to use ownership/Guardian rights, cutoff, reschedule limits, and lifecycle policy; they do not require the Doctor's operational subscription. Replay responses resolve current authorization and current lifecycle state.

`Subscription.PermissionDenied` denotes a missing current actor grant in capability evaluation. `Subscription.FeatureNotEntitled`, `Subscription.AccessLevelInsufficient`, `Subscription.DoctorNotApproved`, and existing expiry codes identify commercial/governance restrictions. HTTP status and opaque resource-not-found conventions are preserved. Standard ProblemDetails `instance` may echo the caller-supplied request URI; error payloads contain no protected resource DTO.

The complete reviewed inventory, including original PR #36 mappings, stable permission GUIDs, actor types, and source references, is [permission-coverage.csv](permission-coverage.csv). Existing databases must rerun the commercial catalog seeder after deployment. No new migration or configuration is required. Prescription PDF export uses the existing `PDF_XLSX_EXPORT` feature; reception account state uses `RECEPTION_MANAGEMENT` Full access.

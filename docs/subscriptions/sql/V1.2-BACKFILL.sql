-- Embedded verbatim in the additive EF migration. Run only through the reviewed migration.
-- Preserve accepted commercial and healthcare rows. Never create a free/paid period.
DECLARE @cutover datetime2(7) = SYSUTCDATETIME();
UPDATE dbo.DoctorSubscriptions SET SubscriptionContractId = Id;

-- A chain link needs the real RenewalRecorded audit and a unique, matching paid successor.
SELECT h.SubscriptionId AS ParentId, TRY_CONVERT(uniqueidentifier, JSON_VALUE(h.MetadataJson, '$.NextSubscriptionId')) AS ChildId
INTO #RecordedLinks
FROM dbo.SubscriptionHistories h
WHERE h.EventType = 'RenewalRecorded' AND ISJSON(h.MetadataJson) = 1;
SELECT l.ParentId, l.ChildId INTO #VerifiedLinks
FROM #RecordedLinks l
JOIN dbo.DoctorSubscriptions p ON p.Id = l.ParentId
JOIN dbo.DoctorSubscriptions c ON c.Id = l.ChildId
WHERE p.DoctorId = c.DoctorId AND p.ProductId = c.ProductId AND p.SubscriptionKind = 1 AND c.SubscriptionKind = 1
AND l.ParentId <> l.ChildId AND c.StartsAtUtc >= p.EndsAtUtc
AND (SELECT COUNT(*) FROM #RecordedLinks x WHERE x.ChildId = l.ChildId) = 1
AND (SELECT COUNT(*) FROM #RecordedLinks x WHERE x.ParentId = l.ParentId) = 1;
;WITH Chain AS (
 SELECT s.Id, s.Id AS RootId, CAST('|' + CONVERT(varchar(36), s.Id) + '|' AS varchar(max)) AS Path
 FROM dbo.DoctorSubscriptions s WHERE NOT EXISTS (SELECT 1 FROM #VerifiedLinks l WHERE l.ChildId = s.Id)
 UNION ALL
 SELECT l.ChildId, c.RootId, CAST(c.Path + CONVERT(varchar(36), l.ChildId) + '|' AS varchar(max))
 FROM Chain c JOIN #VerifiedLinks l ON l.ParentId = c.Id
 WHERE c.Path NOT LIKE '%|' + CONVERT(varchar(36), l.ChildId) + '|%'
)
UPDATE s SET SubscriptionContractId = c.RootId FROM dbo.DoctorSubscriptions s JOIN Chain c ON s.Id = c.Id OPTION (MAXRECURSION 0);
INSERT dbo.SubscriptionContracts (Id, DoctorId, Kind, CreatedOnUtc)
SELECT SubscriptionContractId, DoctorId, SubscriptionKind, MIN(CreatedOnUtc)
FROM dbo.DoctorSubscriptions GROUP BY SubscriptionContractId, DoctorId, SubscriptionKind;
INSERT dbo.SubscriptionMigrationIssues (Id, ResourceId, IssueCode, CreatedOnUtc)
SELECT NEWID(), s.Id, 'RenewalChainNotProven', @cutover FROM dbo.DoctorSubscriptions s
WHERE EXISTS (SELECT 1 FROM #RecordedLinks l WHERE l.ChildId = s.Id)
AND NOT EXISTS (SELECT 1 FROM #VerifiedLinks l WHERE l.ChildId = s.Id);

-- Materialize the old single-subscription entitlement intervals, clipped at supersession.
SELECT s.Id AS PeriodId, s.SubscriptionContractId, s.DoctorId, s.StartsAtUtc,
 CASE WHEN s.Status = 5 AND s.ModifiedOnUtc < s.EndsAtUtc THEN s.ModifiedOnUtc ELSE s.EndsAtUtc END AS EffectiveEndUtc,
 s.Status, s.PracticeLimit, s.ReceptionLimit
INTO #Periods FROM dbo.DoctorSubscriptions s
WHERE s.StartsAtUtc IS NOT NULL AND s.EndsAtUtc IS NOT NULL AND s.Status IN (2,3,5,6);
SELECT NEWID() AS Id, p.Id AS DoctorPracticeId, s.SubscriptionContractId, s.PeriodId,
 CASE WHEN p.CreatedOnUtc > s.StartsAtUtc THEN p.CreatedOnUtc ELSE s.StartsAtUtc END AS AllocatedFromUtc,
 CASE WHEN s.Status IN (2,6) AND s.StartsAtUtc <= @cutover AND @cutover < s.EffectiveEndUtc THEN NULL ELSE s.EffectiveEndUtc END AS ReleasedAtUtc,
 d.ApplicationUserId AS ActorId,
 CAST(CASE WHEN (SELECT COUNT(*) FROM dbo.DoctorPractices x WHERE x.DoctorId = d.Id) > s.PracticeLimit
 OR (SELECT COUNT(DISTINCT r.Id) FROM dbo.Receptions r JOIN dbo.ApplicationUsers u ON u.Id = r.ApplicationUserId
     JOIN dbo.ReceptionPracticeAssignments a ON a.ReceptionId = r.Id WHERE r.OwnerDoctorId = d.Id AND u.IsActive = 1 AND a.IsActive = 1) > s.ReceptionLimit
 THEN 1 ELSE 0 END AS bit) AS LegacyGrandfathered
INTO #Allocations
FROM #Periods s JOIN dbo.Doctors d ON d.Id = s.DoctorId JOIN dbo.DoctorPractices p ON p.DoctorId = d.Id
WHERE p.CreatedOnUtc < s.EffectiveEndUtc AND s.StartsAtUtc < s.EffectiveEndUtc
AND NOT EXISTS (SELECT 1 FROM #Periods other WHERE other.DoctorId = s.DoctorId AND other.PeriodId <> s.PeriodId
 AND other.StartsAtUtc < s.EffectiveEndUtc AND s.StartsAtUtc < other.EffectiveEndUtc);
INSERT dbo.PracticeSubscriptionAllocations (Id, DoctorPracticeId, SubscriptionContractId, AllocatedFromUtc, ReleasedAtUtc,
 CreatedByApplicationUserId, ReleasedByApplicationUserId, ReleaseReason, LegacyGrandfathered)
SELECT Id, DoctorPracticeId, SubscriptionContractId, AllocatedFromUtc, ReleasedAtUtc, ActorId,
 CASE WHEN ReleasedAtUtc IS NULL THEN NULL ELSE ActorId END,
 CASE WHEN ReleasedAtUtc IS NULL THEN NULL ELSE 'Verified pre-v1.2 commercial interval' END, LegacyGrandfathered FROM #Allocations;
INSERT dbo.SubscriptionMigrationIssues (Id, ResourceId, IssueCode, CreatedOnUtc)
SELECT NEWID(), p.Id, 'PracticeWithoutProvenCurrentCoverage', @cutover FROM dbo.DoctorPractices p
WHERE NOT EXISTS (SELECT 1 FROM dbo.PracticeSubscriptionAllocations a WHERE a.DoctorPracticeId = p.Id AND a.ReleasedAtUtc IS NULL);

-- Use the accepted terms from the latest audit at booking time, before any later upgrade.
SELECT r.Id, r.DoctorId, r.DoctorPracticeId, r.CreatedOnUtc, 1 AS OriginKind INTO #Reservations
FROM dbo.Reservations r
UNION ALL SELECT r.Id, r.DoctorId, r.DoctorPracticeId, r.CreatedOnUtc, 1 FROM archive.Reservations r
UNION ALL SELECT t.Id, t.DoctorId, t.DoctorPracticeId, t.CreatedOnUtc, 2 FROM dbo.Tickets t WHERE t.ReservationId IS NULL
UNION ALL SELECT t.Id, t.DoctorId, t.DoctorPracticeId, t.CreatedOnUtc, 2 FROM archive.Tickets t WHERE t.ReservationId IS NULL;
SELECT r.Id, r.DoctorId, r.DoctorPracticeId, r.CreatedOnUtc, r.OriginKind, a.Id AS AllocationId, a.SubscriptionContractId, a.PeriodId,
 COALESCE(h.NewTermsSnapshotJson, CASE WHEN NOT EXISTS (SELECT 1 FROM dbo.SubscriptionHistories later
 WHERE later.SubscriptionId = a.PeriodId AND later.EventType = 'ChangeApproved') THEN s.TermsSnapshotJson END) AS TermsSnapshotJson,
 COUNT(*) OVER (PARTITION BY r.Id) AS Candidates
INTO #ProvenanceCandidates
FROM #Reservations r JOIN #Allocations a ON a.DoctorPracticeId = r.DoctorPracticeId AND a.AllocatedFromUtc <= r.CreatedOnUtc
JOIN #Periods interval ON interval.PeriodId = a.PeriodId AND interval.DoctorId = r.DoctorId AND r.CreatedOnUtc < interval.EffectiveEndUtc
JOIN dbo.DoctorSubscriptions s ON s.Id = a.PeriodId
OUTER APPLY (SELECT TOP (1) history.NewTermsSnapshotJson FROM dbo.SubscriptionHistories history
 WHERE history.SubscriptionId = s.Id AND history.OccurredOnUtc <= r.CreatedOnUtc AND ISJSON(history.NewTermsSnapshotJson) = 1
 ORDER BY history.OccurredOnUtc DESC, history.CreatedOnUtc DESC, history.Id DESC) h;
INSERT dbo.ReservationCommercialProvenances (Id, DoctorId, DoctorPracticeId, AllocationId, SubscriptionContractId, SubscriptionId, AcceptedAtUtc, TermsSnapshotJson, OriginKind)
SELECT Id, DoctorId, DoctorPracticeId, AllocationId, SubscriptionContractId, PeriodId, CreatedOnUtc, TermsSnapshotJson, OriginKind
FROM #ProvenanceCandidates WHERE Candidates = 1 AND ISJSON(TermsSnapshotJson) = 1;
INSERT dbo.SubscriptionMigrationIssues (Id, ResourceId, IssueCode, CreatedOnUtc)
SELECT NEWID(), r.Id, 'OriginalEntitlementNotProven', @cutover FROM #Reservations r
WHERE NOT EXISTS (SELECT 1 FROM dbo.ReservationCommercialProvenances p WHERE p.Id = r.Id);
INSERT dbo.SubscriptionReminderSchedules (Id, SubscriptionId, ReminderCode, DueAtUtc, TrialRevision, Status, CreatedOnUtc)
SELECT NEWID(), s.Id, stage.Code, DATEADD(day, -stage.DaysBeforeEnd, s.EndsAtUtc), 0, 1, @cutover
FROM dbo.DoctorSubscriptions s CROSS JOIN (VALUES ('D7',7),('D3',3),('D1',1),('D0',0)) stage(Code,DaysBeforeEnd)
WHERE s.SubscriptionKind=1 AND s.Status IN (2,6) AND s.EndsAtUtc IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM dbo.SubscriptionReminderSchedules r WHERE r.SubscriptionId=s.Id AND r.ReminderCode=stage.Code AND r.TrialRevision=0);
DROP TABLE #ProvenanceCandidates, #Reservations, #Allocations, #Periods, #VerifiedLinks, #RecordedLinks;

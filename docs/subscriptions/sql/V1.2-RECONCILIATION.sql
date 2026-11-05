-- Read-only pre/post cutover inventory. Execute against a restored non-production copy.
SET NOCOUNT ON;
SELECT 'Doctors' AS Entity, COUNT_BIG(*) AS Rows FROM dbo.Doctors
UNION ALL SELECT 'Practices', COUNT_BIG(*) FROM dbo.DoctorPractices
UNION ALL SELECT 'LiveReservations', COUNT_BIG(*) FROM dbo.Reservations
UNION ALL SELECT 'ArchivedReservations', COUNT_BIG(*) FROM archive.Reservations
UNION ALL SELECT 'LiveTickets', COUNT_BIG(*) FROM dbo.Tickets
UNION ALL SELECT 'ArchivedTickets', COUNT_BIG(*) FROM archive.Tickets
UNION ALL SELECT 'Encounters', COUNT_BIG(*) FROM dbo.MedicalEncounters
UNION ALL SELECT 'SaaSPayments', COUNT_BIG(*) FROM dbo.SubscriptionPayments;
SELECT SubscriptionKind, Status, COUNT_BIG(*) AS Periods, SUM(PriceAmount) AS AcceptedAmount
FROM dbo.DoctorSubscriptions GROUP BY SubscriptionKind, Status;
IF OBJECT_ID('dbo.SubscriptionContracts') IS NOT NULL
BEGIN
 EXEC(N'SELECT Kind, COUNT_BIG(*) AS Contracts FROM dbo.SubscriptionContracts GROUP BY Kind;
 SELECT a.SubscriptionContractId, COUNT_BIG(*) AS AllocatedPractices, SUM(CONVERT(int,a.LegacyGrandfathered)) AS Grandfathered
 FROM dbo.PracticeSubscriptionAllocations a WHERE a.ReleasedAtUtc IS NULL GROUP BY a.SubscriptionContractId;
 SELECT a.SubscriptionContractId, COUNT(DISTINCT r.Id) AS ReceptionSeats
 FROM dbo.PracticeSubscriptionAllocations a JOIN dbo.ReceptionPracticeAssignments assignment ON assignment.DoctorPracticeId=a.DoctorPracticeId
 JOIN dbo.Receptions r ON r.Id=assignment.ReceptionId JOIN dbo.ApplicationUsers u ON u.Id=r.ApplicationUserId
 WHERE a.ReleasedAtUtc IS NULL AND assignment.IsActive=1 AND u.IsActive=1 GROUP BY a.SubscriptionContractId;
 SELECT IssueCode, COUNT_BIG(*) AS Exceptions FROM dbo.SubscriptionMigrationIssues GROUP BY IssueCode;
 SELECT ResourceId,IssueCode FROM dbo.SubscriptionMigrationIssues ORDER BY IssueCode,ResourceId;
 SELECT COUNT_BIG(*) AS UnallocatedPractices FROM dbo.DoctorPractices p WHERE NOT EXISTS
   (SELECT 1 FROM dbo.PracticeSubscriptionAllocations a WHERE a.DoctorPracticeId=p.Id AND a.ReleasedAtUtc IS NULL);
 SELECT OriginKind,COUNT_BIG(*) AS ProvenOrigins FROM dbo.ReservationCommercialProvenances GROUP BY OriginKind;');
END;
SELECT COUNT_BIG(*) AS FutureLiveReservations FROM dbo.Reservations WHERE ScheduledStartUtc > SYSUTCDATETIME();
-- No patient names, contacts, diagnoses, documents or prescription content are returned.

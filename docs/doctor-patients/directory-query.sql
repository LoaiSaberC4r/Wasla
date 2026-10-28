-- SQL emitted by the synthetic SQL Server acceptance test.
-- Parameter values intentionally omitted. This is query text, not an execution plan.
SELECT [w0].[PatientId], [w0].[First], [w0].[Last], [w0].[Count], [w0].[Practices], [p].[Id], [p].[NameAr], [p].[NameEn], [p].[PhoneNumber], [p].[Email], [p].[DateOfBirth], [p].[Gender], [w3].[BusinessDate], [d2].[Id] AS [PracticeId], [d2].[NameAr] AS [PracticeNameAr], [d2].[NameEn] AS [PracticeNameEn], [d2].[IsActive], CASE
    WHEN NOT EXISTS (
        SELECT 1
        FROM (
            SELECT [Id],[BusinessDate],[CallCycle],[CalledOnUtc],[CancellationReason],[CancellationReasonCode],[CancelledOnUtc],[CheckInMode],[CheckInTimeUtc],[CompletedOnUtc],[CreatedByApplicationUserId],[CreatedOnUtc],[DoctorId],[DoctorPracticeId],[FastTrackGrantedOnUtc],[FollowUpEligibilityId],[InProgressOnUtc],[IsFastTrack],[IsOperationallyOpen],[LastUpdatedOnUtc],[ModifiedByApplicationUserId],[ModifiedOnUtc],[NoShowOnUtc],[OperationalFinalizedAtUtc],[PatientId],[PracticeTimeZoneIdSnapshot],[PriceSnapshot],[QueueOrderTimeUtc],[ReservationId],[RowVersion],[SegmentId],[SegmentNameArSnapshot],[SegmentNameEnSnapshot],[SegmentPrioritySnapshot],[Source],[Status],[TicketNumber],[VisitTypeCodeSnapshot],[VisitTypeId],[VisitTypeNameArSnapshot],[VisitTypeNameEnSnapshot] FROM [dbo].[Tickets] WITH (HOLDLOCK) UNION ALL SELECT [Id],[BusinessDate],[CallCycle],[CalledOnUtc],[CancellationReason],[CancellationReasonCode],[CancelledOnUtc],[CheckInMode],[CheckInTimeUtc],[CompletedOnUtc],[CreatedByApplicationUserId],[CreatedOnUtc],[DoctorId],[DoctorPracticeId],[FastTrackGrantedOnUtc],[FollowUpEligibilityId],[InProgressOnUtc],[IsFastTrack],[IsOperationallyOpen],[LastUpdatedOnUtc],[ModifiedByApplicationUserId],[ModifiedOnUtc],[NoShowOnUtc],[OperationalFinalizedAtUtc],[PatientId],[PracticeTimeZoneIdSnapshot],[PriceSnapshot],[QueueOrderTimeUtc],[ReservationId],[RowVersion],[SegmentId],[SegmentNameArSnapshot],[SegmentNameEnSnapshot],[SegmentPrioritySnapshot],[Source],[Status],[TicketNumber],[VisitTypeCodeSnapshot],[VisitTypeId],[VisitTypeNameArSnapshot],[VisitTypeNameEnSnapshot] FROM [archive].[Tickets] WITH (HOLDLOCK)
        ) AS [w4]
        WHERE [w4].[DoctorId] = @doctor30 AND [w4].[TicketNumber] > 0 AND [w4].[CheckInTimeUtc] > '0001-01-01T00:00:00.000' AND [w4].[CreatedByApplicationUserId] <> '00000000-0000-0000-0000-000000000000' AND (([w4].[Source] = 2 AND [w4].[CheckInMode] = 3 AND [w4].[ReservationId] IS NULL) OR ([w4].[Source] = 1 AND [w4].[ReservationId] IS NOT NULL AND ([w4].[CheckInMode] = 1 OR [w4].[CheckInMode] = 2) AND EXISTS (
            SELECT 1
            FROM [ReservationRegistry] AS [r2]
            WHERE [r2].[Id] = [w4].[ReservationId] AND [r2].[DoctorId] = @doctor30 AND [r2].[PatientId] = [w4].[PatientId] AND [r2].[DoctorPracticeId] = [w4].[DoctorPracticeId]))) AND EXISTS (
            SELECT 1
            FROM [TicketRegistry] AS [t2]
            WHERE [t2].[Id] = [w4].[Id] AND [t2].[DoctorId] = @doctor30 AND [t2].[PatientId] = [w4].[PatientId] AND [t2].[DoctorPracticeId] = [w4].[DoctorPracticeId] AND ([t2].[ReservationId] = [w4].[ReservationId] OR ([t2].[ReservationId] IS NULL AND [w4].[ReservationId] IS NULL)) AND [t2].[BusinessDate] = [w4].[BusinessDate] AND [t2].[TicketNumber] = [w4].[TicketNumber]) AND EXISTS (
            SELECT 1
            FROM [DoctorPractices] AS [d3]
            WHERE [d3].[Id] = [w4].[DoctorPracticeId] AND [d3].[DoctorId] = @doctor30) AND [w4].[PatientId] = [p].[Id] AND EXISTS (
            SELECT 1
            FROM [DoctorPractices] AS [d4]
            WHERE [d4].[Id] = [w4].[DoctorPracticeId] AND [d4].[IsActive] = CAST(1 AS bit))) THEN CAST(1 AS bit)
    ELSE CAST(0 AS bit)
END AS [HistoricalOnly], (
    SELECT COUNT(*)
    FROM [MedicalEncounters] AS [m]
    WHERE [m].[DoctorId] = @a_DoctorId AND [m].[PatientId] = [p].[Id] AND [m].[Status] = 2 AND [m].[DoctorPracticeId] IN (
        SELECT [d5].[Id]
        FROM [DoctorPractices] AS [d5]
        WHERE [d5].[DoctorId] = @a_DoctorId39
    )) AS [Completed], CASE
    WHEN EXISTS (
        SELECT 1
        FROM (
            SELECT [Id],[AppointmentRevisionHistoryId],[BookedOnBehalfRelationshipSnapshot],[BookingNote],[BookingSource],[BusinessDate],[CancellationComment],[CancellationInitiator],[CancellationReasonCode],[CancelledByApplicationUserId],[CancelledOnUtc],[CheckInGracePeriodMinutesSnapshot],[ConvertedToTicketOnUtc],[CreatedByApplicationUserId],[CreatedOnUtc],[DoctorId],[DoctorPracticeId],[ExpiredOnUtc],[FollowUpEligibilityId],[LastNoShowOnUtc],[LastNoShowRestoredOnUtc],[LastRescheduledOnUtc],[ModifiedByApplicationUserId],[ModifiedOnUtc],[PatientId],[PatientInitiatedRescheduleCount],[PriceSnapshot],[ReservationReference],[RowVersion],[ScheduledLocalDateTime],[ScheduledStartUtc],[SegmentId],[SegmentNameArSnapshot],[SegmentNameEnSnapshot],[SegmentPrioritySnapshot],[SlotDurationMinutesSnapshot],[Status],[TimeZoneIdSnapshot],[VisitTypeCodeSnapshot],[VisitTypeId],[VisitTypeNameArSnapshot],[VisitTypeNameEnSnapshot] FROM [dbo].[Reservations] WITH (HOLDLOCK) UNION ALL SELECT [Id],[AppointmentRevisionHistoryId],[BookedOnBehalfRelationshipSnapshot],[BookingNote],[BookingSource],[BusinessDate],[CancellationComment],[CancellationInitiator],[CancellationReasonCode],[CancelledByApplicationUserId],[CancelledOnUtc],[CheckInGracePeriodMinutesSnapshot],[ConvertedToTicketOnUtc],[CreatedByApplicationUserId],[CreatedOnUtc],[DoctorId],[DoctorPracticeId],[ExpiredOnUtc],[FollowUpEligibilityId],[LastNoShowOnUtc],[LastNoShowRestoredOnUtc],[LastRescheduledOnUtc],[ModifiedByApplicationUserId],[ModifiedOnUtc],[PatientId],[PatientInitiatedRescheduleCount],[PriceSnapshot],[ReservationReference],[RowVersion],[ScheduledLocalDateTime],[ScheduledStartUtc],[SegmentId],[SegmentNameArSnapshot],[SegmentNameEnSnapshot],[SegmentPrioritySnapshot],[SlotDurationMinutesSnapshot],[Status],[TimeZoneIdSnapshot],[VisitTypeCodeSnapshot],[VisitTypeId],[VisitTypeNameArSnapshot],[VisitTypeNameEnSnapshot] FROM [archive].[Reservations] WITH (HOLDLOCK)
        ) AS [w5]
        WHERE [w5].[DoctorId] = @a_DoctorId44 AND [w5].[DoctorPracticeId] IN (
            SELECT [d6].[Id]
            FROM [DoctorPractices] AS [d6]
            WHERE [d6].[DoctorId] = @a_DoctorId46
        ) AND EXISTS (
            SELECT 1
            FROM [ReservationRegistry] AS [r3]
            WHERE [r3].[Id] = [w5].[Id] AND [r3].[DoctorId] = @a_DoctorId44 AND [r3].[PatientId] = [w5].[PatientId] AND [r3].[DoctorPracticeId] = [w5].[DoctorPracticeId]) AND [w5].[PatientId] = [p].[Id] AND [w5].[Status] = 1 AND [w5].[ScheduledStartUtc] > @now) THEN CAST(1 AS bit)
    ELSE CAST(0 AS bit)
END AS [Upcoming]
FROM (
    SELECT [w].[PatientId], MIN([w].[CheckInTimeUtc]) AS [First], MAX([w].[CheckInTimeUtc]) AS [Last], COUNT(*) AS [Count], COUNT(DISTINCT ([w].[DoctorPracticeId])) AS [Practices]
    FROM (
        SELECT [Id],[BusinessDate],[CallCycle],[CalledOnUtc],[CancellationReason],[CancellationReasonCode],[CancelledOnUtc],[CheckInMode],[CheckInTimeUtc],[CompletedOnUtc],[CreatedByApplicationUserId],[CreatedOnUtc],[DoctorId],[DoctorPracticeId],[FastTrackGrantedOnUtc],[FollowUpEligibilityId],[InProgressOnUtc],[IsFastTrack],[IsOperationallyOpen],[LastUpdatedOnUtc],[ModifiedByApplicationUserId],[ModifiedOnUtc],[NoShowOnUtc],[OperationalFinalizedAtUtc],[PatientId],[PracticeTimeZoneIdSnapshot],[PriceSnapshot],[QueueOrderTimeUtc],[ReservationId],[RowVersion],[SegmentId],[SegmentNameArSnapshot],[SegmentNameEnSnapshot],[SegmentPrioritySnapshot],[Source],[Status],[TicketNumber],[VisitTypeCodeSnapshot],[VisitTypeId],[VisitTypeNameArSnapshot],[VisitTypeNameEnSnapshot] FROM [dbo].[Tickets] WITH (HOLDLOCK) UNION ALL SELECT [Id],[BusinessDate],[CallCycle],[CalledOnUtc],[CancellationReason],[CancellationReasonCode],[CancelledOnUtc],[CheckInMode],[CheckInTimeUtc],[CompletedOnUtc],[CreatedByApplicationUserId],[CreatedOnUtc],[DoctorId],[DoctorPracticeId],[FastTrackGrantedOnUtc],[FollowUpEligibilityId],[InProgressOnUtc],[IsFastTrack],[IsOperationallyOpen],[LastUpdatedOnUtc],[ModifiedByApplicationUserId],[ModifiedOnUtc],[NoShowOnUtc],[OperationalFinalizedAtUtc],[PatientId],[PracticeTimeZoneIdSnapshot],[PriceSnapshot],[QueueOrderTimeUtc],[ReservationId],[RowVersion],[SegmentId],[SegmentNameArSnapshot],[SegmentNameEnSnapshot],[SegmentPrioritySnapshot],[Source],[Status],[TicketNumber],[VisitTypeCodeSnapshot],[VisitTypeId],[VisitTypeNameArSnapshot],[VisitTypeNameEnSnapshot] FROM [archive].[Tickets] WITH (HOLDLOCK)
    ) AS [w]
    WHERE [w].[DoctorId] = @doctor AND [w].[TicketNumber] > 0 AND [w].[CheckInTimeUtc] > '0001-01-01T00:00:00.000' AND [w].[CreatedByApplicationUserId] <> '00000000-0000-0000-0000-000000000000' AND (([w].[Source] = 2 AND [w].[CheckInMode] = 3 AND [w].[ReservationId] IS NULL) OR ([w].[Source] = 1 AND [w].[ReservationId] IS NOT NULL AND [w].[CheckInMode] IN (1, 2) AND EXISTS (
        SELECT 1
        FROM [ReservationRegistry] AS [r]
        WHERE [r].[Id] = [w].[ReservationId] AND [r].[DoctorId] = @doctor AND [r].[PatientId] = [w].[PatientId] AND [r].[DoctorPracticeId] = [w].[DoctorPracticeId]))) AND EXISTS (
        SELECT 1
        FROM [TicketRegistry] AS [t]
        WHERE [t].[Id] = [w].[Id] AND [t].[DoctorId] = @doctor AND [t].[PatientId] = [w].[PatientId] AND [t].[DoctorPracticeId] = [w].[DoctorPracticeId] AND ([t].[ReservationId] = [w].[ReservationId] OR ([t].[ReservationId] IS NULL AND [w].[ReservationId] IS NULL)) AND [t].[BusinessDate] = [w].[BusinessDate] AND [t].[TicketNumber] = [w].[TicketNumber]) AND EXISTS (
        SELECT 1
        FROM [DoctorPractices] AS [d]
        WHERE [d].[Id] = [w].[DoctorPracticeId] AND [d].[DoctorId] = @doctor) AND [w].[CheckInTimeUtc] <= @asOf
    GROUP BY [w].[PatientId]
) AS [w0]
INNER JOIN [Patients] AS [p] ON [w0].[PatientId] = [p].[Id]
INNER JOIN (
    SELECT [w1].[Id], [w1].[BusinessDate], [w1].[DoctorPracticeId]
    FROM (
        SELECT [Id],[BusinessDate],[CallCycle],[CalledOnUtc],[CancellationReason],[CancellationReasonCode],[CancelledOnUtc],[CheckInMode],[CheckInTimeUtc],[CompletedOnUtc],[CreatedByApplicationUserId],[CreatedOnUtc],[DoctorId],[DoctorPracticeId],[FastTrackGrantedOnUtc],[FollowUpEligibilityId],[InProgressOnUtc],[IsFastTrack],[IsOperationallyOpen],[LastUpdatedOnUtc],[ModifiedByApplicationUserId],[ModifiedOnUtc],[NoShowOnUtc],[OperationalFinalizedAtUtc],[PatientId],[PracticeTimeZoneIdSnapshot],[PriceSnapshot],[QueueOrderTimeUtc],[ReservationId],[RowVersion],[SegmentId],[SegmentNameArSnapshot],[SegmentNameEnSnapshot],[SegmentPrioritySnapshot],[Source],[Status],[TicketNumber],[VisitTypeCodeSnapshot],[VisitTypeId],[VisitTypeNameArSnapshot],[VisitTypeNameEnSnapshot] FROM [dbo].[Tickets] WITH (HOLDLOCK) UNION ALL SELECT [Id],[BusinessDate],[CallCycle],[CalledOnUtc],[CancellationReason],[CancellationReasonCode],[CancelledOnUtc],[CheckInMode],[CheckInTimeUtc],[CompletedOnUtc],[CreatedByApplicationUserId],[CreatedOnUtc],[DoctorId],[DoctorPracticeId],[FastTrackGrantedOnUtc],[FollowUpEligibilityId],[InProgressOnUtc],[IsFastTrack],[IsOperationallyOpen],[LastUpdatedOnUtc],[ModifiedByApplicationUserId],[ModifiedOnUtc],[NoShowOnUtc],[OperationalFinalizedAtUtc],[PatientId],[PracticeTimeZoneIdSnapshot],[PriceSnapshot],[QueueOrderTimeUtc],[ReservationId],[RowVersion],[SegmentId],[SegmentNameArSnapshot],[SegmentNameEnSnapshot],[SegmentPrioritySnapshot],[Source],[Status],[TicketNumber],[VisitTypeCodeSnapshot],[VisitTypeId],[VisitTypeNameArSnapshot],[VisitTypeNameEnSnapshot] FROM [archive].[Tickets] WITH (HOLDLOCK)
    ) AS [w1]
    WHERE [w1].[DoctorId] = @doctor AND [w1].[TicketNumber] > 0 AND [w1].[CheckInTimeUtc] > '0001-01-01T00:00:00.000' AND [w1].[CreatedByApplicationUserId] <> '00000000-0000-0000-0000-000000000000' AND (([w1].[Source] = 2 AND [w1].[CheckInMode] = 3 AND [w1].[ReservationId] IS NULL) OR ([w1].[Source] = 1 AND [w1].[ReservationId] IS NOT NULL AND ([w1].[CheckInMode] = 1 OR [w1].[CheckInMode] = 2) AND EXISTS (
        SELECT 1
        FROM [ReservationRegistry] AS [r0]
        WHERE [r0].[Id] = [w1].[ReservationId] AND [r0].[DoctorId] = @doctor AND [r0].[PatientId] = [w1].[PatientId] AND [r0].[DoctorPracticeId] = [w1].[DoctorPracticeId]))) AND EXISTS (
        SELECT 1
        FROM [TicketRegistry] AS [t0]
        WHERE [t0].[Id] = [w1].[Id] AND [t0].[DoctorId] = @doctor AND [t0].[PatientId] = [w1].[PatientId] AND [t0].[DoctorPracticeId] = [w1].[DoctorPracticeId] AND ([t0].[ReservationId] = [w1].[ReservationId] OR ([t0].[ReservationId] IS NULL AND [w1].[ReservationId] IS NULL)) AND [t0].[BusinessDate] = [w1].[BusinessDate] AND [t0].[TicketNumber] = [w1].[TicketNumber]) AND EXISTS (
        SELECT 1
        FROM [DoctorPractices] AS [d0]
        WHERE [d0].[Id] = [w1].[DoctorPracticeId] AND [d0].[DoctorId] = @doctor) AND [w1].[CheckInTimeUtc] <= @asOf
) AS [w3] ON (
    SELECT TOP(1) [w2].[Id]
    FROM (
        SELECT [Id],[BusinessDate],[CallCycle],[CalledOnUtc],[CancellationReason],[CancellationReasonCode],[CancelledOnUtc],[CheckInMode],[CheckInTimeUtc],[CompletedOnUtc],[CreatedByApplicationUserId],[CreatedOnUtc],[DoctorId],[DoctorPracticeId],[FastTrackGrantedOnUtc],[FollowUpEligibilityId],[InProgressOnUtc],[IsFastTrack],[IsOperationallyOpen],[LastUpdatedOnUtc],[ModifiedByApplicationUserId],[ModifiedOnUtc],[NoShowOnUtc],[OperationalFinalizedAtUtc],[PatientId],[PracticeTimeZoneIdSnapshot],[PriceSnapshot],[QueueOrderTimeUtc],[ReservationId],[RowVersion],[SegmentId],[SegmentNameArSnapshot],[SegmentNameEnSnapshot],[SegmentPrioritySnapshot],[Source],[Status],[TicketNumber],[VisitTypeCodeSnapshot],[VisitTypeId],[VisitTypeNameArSnapshot],[VisitTypeNameEnSnapshot] FROM [dbo].[Tickets] WITH (HOLDLOCK) UNION ALL SELECT [Id],[BusinessDate],[CallCycle],[CalledOnUtc],[CancellationReason],[CancellationReasonCode],[CancelledOnUtc],[CheckInMode],[CheckInTimeUtc],[CompletedOnUtc],[CreatedByApplicationUserId],[CreatedOnUtc],[DoctorId],[DoctorPracticeId],[FastTrackGrantedOnUtc],[FollowUpEligibilityId],[InProgressOnUtc],[IsFastTrack],[IsOperationallyOpen],[LastUpdatedOnUtc],[ModifiedByApplicationUserId],[ModifiedOnUtc],[NoShowOnUtc],[OperationalFinalizedAtUtc],[PatientId],[PracticeTimeZoneIdSnapshot],[PriceSnapshot],[QueueOrderTimeUtc],[ReservationId],[RowVersion],[SegmentId],[SegmentNameArSnapshot],[SegmentNameEnSnapshot],[SegmentPrioritySnapshot],[Source],[Status],[TicketNumber],[VisitTypeCodeSnapshot],[VisitTypeId],[VisitTypeNameArSnapshot],[VisitTypeNameEnSnapshot] FROM [archive].[Tickets] WITH (HOLDLOCK)
    ) AS [w2]
    WHERE [w2].[DoctorId] = @doctor AND [w2].[TicketNumber] > 0 AND [w2].[CheckInTimeUtc] > '0001-01-01T00:00:00.000' AND [w2].[CreatedByApplicationUserId] <> '00000000-0000-0000-0000-000000000000' AND (([w2].[Source] = 2 AND [w2].[CheckInMode] = 3 AND [w2].[ReservationId] IS NULL) OR ([w2].[Source] = 1 AND [w2].[ReservationId] IS NOT NULL AND ([w2].[CheckInMode] = 1 OR [w2].[CheckInMode] = 2) AND EXISTS (
        SELECT 1
        FROM [ReservationRegistry] AS [r1]
        WHERE [r1].[Id] = [w2].[ReservationId] AND [r1].[DoctorId] = @doctor AND [r1].[PatientId] = [w2].[PatientId] AND [r1].[DoctorPracticeId] = [w2].[DoctorPracticeId]))) AND EXISTS (
        SELECT 1
        FROM [TicketRegistry] AS [t1]
        WHERE [t1].[Id] = [w2].[Id] AND [t1].[DoctorId] = @doctor AND [t1].[PatientId] = [w2].[PatientId] AND [t1].[DoctorPracticeId] = [w2].[DoctorPracticeId] AND ([t1].[ReservationId] = [w2].[ReservationId] OR ([t1].[ReservationId] IS NULL AND [w2].[ReservationId] IS NULL)) AND [t1].[BusinessDate] = [w2].[BusinessDate] AND [t1].[TicketNumber] = [w2].[TicketNumber]) AND EXISTS (
        SELECT 1
        FROM [DoctorPractices] AS [d1]
        WHERE [d1].[Id] = [w2].[DoctorPracticeId] AND [d1].[DoctorId] = @doctor) AND [w2].[CheckInTimeUtc] <= @asOf AND [w2].[PatientId] = [w0].[PatientId]
    ORDER BY [w2].[CheckInTimeUtc] DESC, [w2].[Id]) = [w3].[Id]
INNER JOIN [DoctorPractices] AS [d2] ON [w3].[DoctorPracticeId] = [d2].[Id]
ORDER BY [w0].[Last] DESC, [p].[Id]
OFFSET @p49 ROWS FETCH NEXT @p50 ROWS ONLY
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    DROP INDEX [UX_OneActiveSubscriptionPerDoctor] ON [DoctorSubscriptions];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    DROP INDEX [UX_OnePendingSubscriptionPerDoctor] ON [DoctorSubscriptions];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    DROP INDEX [UX_OneScheduledSubscriptionPerDoctor] ON [DoctorSubscriptions];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    ALTER TABLE [SubscriptionChangeRequests] ADD [RetainedAllocationsJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    ALTER TABLE [DoctorSubscriptions] ADD [SubscriptionContractId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE TABLE [SubscriptionAllocationSelections] (
        [Id] uniqueidentifier NOT NULL,
        [SubscriptionId] uniqueidentifier NOT NULL,
        [SelectionJson] nvarchar(max) NOT NULL,
        [CreatedByApplicationUserId] uniqueidentifier NOT NULL,
        [CreatedOnUtc] datetime2(7) NOT NULL,
        [AppliedOnUtc] datetime2(7) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SubscriptionAllocationSelections] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_SubscriptionAllocationSelections_SubscriptionId] UNIQUE ([SubscriptionId]),
        CONSTRAINT [FK_SubscriptionAllocationSelections_ApplicationUsers_CreatedByApplicationUserId] FOREIGN KEY ([CreatedByApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SubscriptionAllocationSelections_DoctorSubscriptions_SubscriptionId] FOREIGN KEY ([SubscriptionId]) REFERENCES [DoctorSubscriptions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE TABLE [SubscriptionContracts] (
        [Id] uniqueidentifier NOT NULL,
        [DoctorId] uniqueidentifier NOT NULL,
        [Kind] int NOT NULL,
        [CreatedOnUtc] datetime2(7) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SubscriptionContracts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SubscriptionContracts_Doctors_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [Doctors] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE TABLE [SubscriptionMigrationIssues] (
        [Id] uniqueidentifier NOT NULL,
        [ResourceId] uniqueidentifier NOT NULL,
        [IssueCode] nvarchar(200) NOT NULL,
        [CreatedOnUtc] datetime2(7) NOT NULL,
        CONSTRAINT [PK_SubscriptionMigrationIssues] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE TABLE [SubscriptionMutationReceipts] (
        [Id] uniqueidentifier NOT NULL,
        [IdempotencyKey] nvarchar(150) NOT NULL,
        [ActorId] uniqueidentifier NOT NULL,
        [Fingerprint] nvarchar(64) NOT NULL,
        [ResultId] uniqueidentifier NOT NULL,
        [CreatedOnUtc] datetime2(7) NOT NULL,
        CONSTRAINT [PK_SubscriptionMutationReceipts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SubscriptionMutationReceipts_ApplicationUsers_ActorId] FOREIGN KEY ([ActorId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE TABLE [SubscriptionRetainedReceptions] (
        [SubscriptionId] uniqueidentifier NOT NULL,
        [DoctorPracticeId] uniqueidentifier NOT NULL,
        [ReceptionId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SubscriptionRetainedReceptions] PRIMARY KEY ([SubscriptionId], [DoctorPracticeId], [ReceptionId]),
        CONSTRAINT [FK_SubscriptionRetainedReceptions_DoctorPractices_DoctorPracticeId] FOREIGN KEY ([DoctorPracticeId]) REFERENCES [DoctorPractices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SubscriptionRetainedReceptions_Receptions_ReceptionId] FOREIGN KEY ([ReceptionId]) REFERENCES [Receptions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SubscriptionRetainedReceptions_SubscriptionAllocationSelections_SubscriptionId] FOREIGN KEY ([SubscriptionId]) REFERENCES [SubscriptionAllocationSelections] ([SubscriptionId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE TABLE [PracticeSubscriptionAllocations] (
        [Id] uniqueidentifier NOT NULL,
        [DoctorPracticeId] uniqueidentifier NOT NULL,
        [SubscriptionContractId] uniqueidentifier NOT NULL,
        [AllocatedFromUtc] datetime2(7) NOT NULL,
        [ReleasedAtUtc] datetime2(7) NULL,
        [CreatedByApplicationUserId] uniqueidentifier NOT NULL,
        [ReleasedByApplicationUserId] uniqueidentifier NULL,
        [ReleaseReason] nvarchar(2000) NULL,
        [LegacyGrandfathered] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PracticeSubscriptionAllocations] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PracticeSubscriptionAllocations_Time] CHECK ([ReleasedAtUtc] IS NULL OR [ReleasedAtUtc] >= [AllocatedFromUtc]),
        CONSTRAINT [FK_PracticeSubscriptionAllocations_ApplicationUsers_CreatedByApplicationUserId] FOREIGN KEY ([CreatedByApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PracticeSubscriptionAllocations_ApplicationUsers_ReleasedByApplicationUserId] FOREIGN KEY ([ReleasedByApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PracticeSubscriptionAllocations_DoctorPractices_DoctorPracticeId] FOREIGN KEY ([DoctorPracticeId]) REFERENCES [DoctorPractices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PracticeSubscriptionAllocations_SubscriptionContracts_SubscriptionContractId] FOREIGN KEY ([SubscriptionContractId]) REFERENCES [SubscriptionContracts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE TABLE [ReservationCommercialProvenances] (
        [OriginKind] int NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [DoctorId] uniqueidentifier NOT NULL,
        [DoctorPracticeId] uniqueidentifier NOT NULL,
        [AllocationId] uniqueidentifier NOT NULL,
        [SubscriptionContractId] uniqueidentifier NOT NULL,
        [SubscriptionId] uniqueidentifier NOT NULL,
        [AcceptedAtUtc] datetime2(7) NOT NULL,
        [TermsSnapshotJson] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_ReservationCommercialProvenances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReservationCommercialProvenances_DoctorPractices_DoctorPracticeId] FOREIGN KEY ([DoctorPracticeId]) REFERENCES [DoctorPractices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReservationCommercialProvenances_DoctorSubscriptions_SubscriptionId] FOREIGN KEY ([SubscriptionId]) REFERENCES [DoctorSubscriptions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReservationCommercialProvenances_Doctors_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [Doctors] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReservationCommercialProvenances_PracticeSubscriptionAllocations_AllocationId] FOREIGN KEY ([AllocationId]) REFERENCES [PracticeSubscriptionAllocations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReservationCommercialProvenances_SubscriptionContracts_SubscriptionContractId] FOREIGN KEY ([SubscriptionContractId]) REFERENCES [SubscriptionContracts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE TABLE [SubscriptionRetainedPractices] (
        [SubscriptionId] uniqueidentifier NOT NULL,
        [DoctorPracticeId] uniqueidentifier NOT NULL,
        [AllocationId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SubscriptionRetainedPractices] PRIMARY KEY ([SubscriptionId], [DoctorPracticeId]),
        CONSTRAINT [FK_SubscriptionRetainedPractices_DoctorPractices_DoctorPracticeId] FOREIGN KEY ([DoctorPracticeId]) REFERENCES [DoctorPractices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SubscriptionRetainedPractices_PracticeSubscriptionAllocations_AllocationId] FOREIGN KEY ([AllocationId]) REFERENCES [PracticeSubscriptionAllocations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SubscriptionRetainedPractices_SubscriptionAllocationSelections_SubscriptionId] FOREIGN KEY ([SubscriptionId]) REFERENCES [SubscriptionAllocationSelections] ([SubscriptionId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    EXEC(N'-- Embedded verbatim in the additive EF migration. Run only through the reviewed migration.
    -- Preserve accepted commercial and healthcare rows. Never create a free/paid period.
    DECLARE @cutover datetime2(7) = SYSUTCDATETIME();
    UPDATE dbo.DoctorSubscriptions SET SubscriptionContractId = Id;

    -- A chain link needs the real RenewalRecorded audit and a unique, matching paid successor.
    SELECT h.SubscriptionId AS ParentId, TRY_CONVERT(uniqueidentifier, JSON_VALUE(h.MetadataJson, ''$.NextSubscriptionId'')) AS ChildId
    INTO #RecordedLinks
    FROM dbo.SubscriptionHistories h
    WHERE h.EventType = ''RenewalRecorded'' AND ISJSON(h.MetadataJson) = 1;
    SELECT l.ParentId, l.ChildId INTO #VerifiedLinks
    FROM #RecordedLinks l
    JOIN dbo.DoctorSubscriptions p ON p.Id = l.ParentId
    JOIN dbo.DoctorSubscriptions c ON c.Id = l.ChildId
    WHERE p.DoctorId = c.DoctorId AND p.ProductId = c.ProductId AND p.SubscriptionKind = 1 AND c.SubscriptionKind = 1
    AND l.ParentId <> l.ChildId AND c.StartsAtUtc >= p.EndsAtUtc
    AND (SELECT COUNT(*) FROM #RecordedLinks x WHERE x.ChildId = l.ChildId) = 1
    AND (SELECT COUNT(*) FROM #RecordedLinks x WHERE x.ParentId = l.ParentId) = 1;
    ;WITH Chain AS (
     SELECT s.Id, s.Id AS RootId, CAST(''|'' + CONVERT(varchar(36), s.Id) + ''|'' AS varchar(max)) AS Path
     FROM dbo.DoctorSubscriptions s WHERE NOT EXISTS (SELECT 1 FROM #VerifiedLinks l WHERE l.ChildId = s.Id)
     UNION ALL
     SELECT l.ChildId, c.RootId, CAST(c.Path + CONVERT(varchar(36), l.ChildId) + ''|'' AS varchar(max))
     FROM Chain c JOIN #VerifiedLinks l ON l.ParentId = c.Id
     WHERE c.Path NOT LIKE ''%|'' + CONVERT(varchar(36), l.ChildId) + ''|%''
    )
    UPDATE s SET SubscriptionContractId = c.RootId FROM dbo.DoctorSubscriptions s JOIN Chain c ON s.Id = c.Id OPTION (MAXRECURSION 0);
    INSERT dbo.SubscriptionContracts (Id, DoctorId, Kind, CreatedOnUtc)
    SELECT SubscriptionContractId, DoctorId, SubscriptionKind, MIN(CreatedOnUtc)
    FROM dbo.DoctorSubscriptions GROUP BY SubscriptionContractId, DoctorId, SubscriptionKind;
    INSERT dbo.SubscriptionMigrationIssues (Id, ResourceId, IssueCode, CreatedOnUtc)
    SELECT NEWID(), s.Id, ''RenewalChainNotProven'', @cutover FROM dbo.DoctorSubscriptions s
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
     CASE WHEN ReleasedAtUtc IS NULL THEN NULL ELSE ''Verified pre-v1.2 commercial interval'' END, LegacyGrandfathered FROM #Allocations;
    INSERT dbo.SubscriptionMigrationIssues (Id, ResourceId, IssueCode, CreatedOnUtc)
    SELECT NEWID(), p.Id, ''PracticeWithoutProvenCurrentCoverage'', @cutover FROM dbo.DoctorPractices p
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PracticeSubscriptionAllocations a WHERE a.DoctorPracticeId = p.Id AND a.ReleasedAtUtc IS NULL);

    -- Use the accepted terms from the latest audit at booking time, before any later upgrade.
    SELECT r.Id, r.DoctorId, r.DoctorPracticeId, r.CreatedOnUtc, 1 AS OriginKind INTO #Reservations
    FROM dbo.Reservations r
    UNION ALL SELECT r.Id, r.DoctorId, r.DoctorPracticeId, r.CreatedOnUtc, 1 FROM archive.Reservations r
    UNION ALL SELECT t.Id, t.DoctorId, t.DoctorPracticeId, t.CreatedOnUtc, 2 FROM dbo.Tickets t WHERE t.ReservationId IS NULL
    UNION ALL SELECT t.Id, t.DoctorId, t.DoctorPracticeId, t.CreatedOnUtc, 2 FROM archive.Tickets t WHERE t.ReservationId IS NULL;
    SELECT r.Id, r.DoctorId, r.DoctorPracticeId, r.CreatedOnUtc, r.OriginKind, a.Id AS AllocationId, a.SubscriptionContractId, a.PeriodId,
     COALESCE(h.NewTermsSnapshotJson, CASE WHEN NOT EXISTS (SELECT 1 FROM dbo.SubscriptionHistories later
     WHERE later.SubscriptionId = a.PeriodId AND later.EventType = ''ChangeApproved'') THEN s.TermsSnapshotJson END) AS TermsSnapshotJson,
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
    SELECT NEWID(), r.Id, ''OriginalEntitlementNotProven'', @cutover FROM #Reservations r
    WHERE NOT EXISTS (SELECT 1 FROM dbo.ReservationCommercialProvenances p WHERE p.Id = r.Id);
    INSERT dbo.SubscriptionReminderSchedules (Id, SubscriptionId, ReminderCode, DueAtUtc, TrialRevision, Status, CreatedOnUtc)
    SELECT NEWID(), s.Id, stage.Code, DATEADD(day, -stage.DaysBeforeEnd, s.EndsAtUtc), 0, 1, @cutover
    FROM dbo.DoctorSubscriptions s CROSS JOIN (VALUES (''D7'',7),(''D3'',3),(''D1'',1),(''D0'',0)) stage(Code,DaysBeforeEnd)
    WHERE s.SubscriptionKind=1 AND s.Status IN (2,6) AND s.EndsAtUtc IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM dbo.SubscriptionReminderSchedules r WHERE r.SubscriptionId=s.Id AND r.ReminderCode=stage.Code AND r.TrialRevision=0);
    DROP TABLE #ProvenanceCandidates, #Reservations, #Allocations, #Periods, #VerifiedLinks, #RecordedLinks;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE INDEX [IX_DoctorSubscriptions_SubscriptionContractId_StartsAtUtc_EndsAtUtc] ON [DoctorSubscriptions] ([SubscriptionContractId], [StartsAtUtc], [EndsAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_OneActivePeriodPerContract] ON [DoctorSubscriptions] ([SubscriptionContractId]) WHERE [Status] = 2');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_OnePendingPeriodPerContract] ON [DoctorSubscriptions] ([SubscriptionContractId]) WHERE [Status] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_OneScheduledPeriodPerContract] ON [DoctorSubscriptions] ([SubscriptionContractId]) WHERE [Status] = 6');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE INDEX [IX_PracticeSubscriptionAllocations_CreatedByApplicationUserId] ON [PracticeSubscriptionAllocations] ([CreatedByApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE INDEX [IX_PracticeSubscriptionAllocations_ReleasedByApplicationUserId] ON [PracticeSubscriptionAllocations] ([ReleasedByApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE INDEX [IX_PracticeSubscriptionAllocations_SubscriptionContractId_ReleasedAtUtc] ON [PracticeSubscriptionAllocations] ([SubscriptionContractId], [ReleasedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_OneCurrentAllocationPerPractice] ON [PracticeSubscriptionAllocations] ([DoctorPracticeId]) WHERE [ReleasedAtUtc] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE INDEX [IX_ReservationCommercialProvenances_AllocationId] ON [ReservationCommercialProvenances] ([AllocationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE INDEX [IX_ReservationCommercialProvenances_DoctorId] ON [ReservationCommercialProvenances] ([DoctorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE INDEX [IX_ReservationCommercialProvenances_DoctorPracticeId_SubscriptionId] ON [ReservationCommercialProvenances] ([DoctorPracticeId], [SubscriptionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE INDEX [IX_ReservationCommercialProvenances_SubscriptionContractId] ON [ReservationCommercialProvenances] ([SubscriptionContractId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE INDEX [IX_ReservationCommercialProvenances_SubscriptionId] ON [ReservationCommercialProvenances] ([SubscriptionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE INDEX [IX_SubscriptionAllocationSelections_CreatedByApplicationUserId] ON [SubscriptionAllocationSelections] ([CreatedByApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE INDEX [IX_SubscriptionContracts_DoctorId_Kind] ON [SubscriptionContracts] ([DoctorId], [Kind]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SubscriptionMigrationIssues_ResourceId_IssueCode] ON [SubscriptionMigrationIssues] ([ResourceId], [IssueCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE INDEX [IX_SubscriptionMutationReceipts_ActorId] ON [SubscriptionMutationReceipts] ([ActorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SubscriptionMutationReceipts_IdempotencyKey] ON [SubscriptionMutationReceipts] ([IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE INDEX [IX_SubscriptionRetainedPractices_AllocationId] ON [SubscriptionRetainedPractices] ([AllocationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE INDEX [IX_SubscriptionRetainedPractices_DoctorPracticeId] ON [SubscriptionRetainedPractices] ([DoctorPracticeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE INDEX [IX_SubscriptionRetainedReceptions_DoctorPracticeId] ON [SubscriptionRetainedReceptions] ([DoctorPracticeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    CREATE INDEX [IX_SubscriptionRetainedReceptions_ReceptionId] ON [SubscriptionRetainedReceptions] ([ReceptionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    ALTER TABLE [DoctorSubscriptions] ADD CONSTRAINT [FK_DoctorSubscriptions_SubscriptionContracts_SubscriptionContractId] FOREIGN KEY ([SubscriptionContractId]) REFERENCES [SubscriptionContracts] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_DoctorSubscriptions_NoPeriodOverlap ON dbo.DoctorSubscriptions
    AFTER INSERT, UPDATE AS
    BEGIN
      SET NOCOUNT ON;
      IF EXISTS (SELECT 1 FROM inserted n JOIN dbo.DoctorSubscriptions other WITH (UPDLOCK,HOLDLOCK)
        ON other.SubscriptionContractId=n.SubscriptionContractId AND other.Id<>n.Id
        WHERE n.Status IN (2,6) AND other.Status IN (2,6)
          AND n.StartsAtUtc<other.EndsAtUtc AND other.StartsAtUtc<n.EndsAtUtc)
        THROW 51063, ''Commercial periods overlap in the same contract.'', 1;
    END');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111042_CommercialContractsAndPracticeAllocations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261105111042_CommercialContractsAndPracticeAllocations', N'10.0.10');
END;

COMMIT;
GO

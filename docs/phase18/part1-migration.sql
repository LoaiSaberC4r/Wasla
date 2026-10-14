BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    ALTER TABLE [Receptions] ADD CONSTRAINT [AK_Receptions_OwnerDoctorId_Id] UNIQUE ([OwnerDoctorId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    ALTER TABLE [DoctorPractices] ADD CONSTRAINT [AK_DoctorPractices_DoctorId_Id] UNIQUE ([DoctorId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE TABLE [OperatingFinanceHistories] (
        [Id] uniqueidentifier NOT NULL,
        [DoctorId] uniqueidentifier NOT NULL,
        [ResourceType] nvarchar(40) NOT NULL,
        [ResourceId] uniqueidentifier NOT NULL,
        [Operation] nvarchar(80) NOT NULL,
        [Reason] nvarchar(2000) NULL,
        [ActorApplicationUserId] uniqueidentifier NOT NULL,
        [RecordedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_OperatingFinanceHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OperatingFinanceHistories_ApplicationUsers_ActorApplicationUserId] FOREIGN KEY ([ActorApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OperatingFinanceHistories_Doctors_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [Doctors] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE TABLE [OperatingFinancialPlans] (
        [Id] uniqueidentifier NOT NULL,
        [DoctorId] uniqueidentifier NOT NULL,
        [ReceptionId] uniqueidentifier NULL,
        [Kind] int NOT NULL,
        [Status] int NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        [FirstCoveredMonth] date NOT NULL,
        [CurrentRevisionNumber] int NOT NULL,
        [CreatedByApplicationUserId] uniqueidentifier NOT NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [ModifiedByApplicationUserId] uniqueidentifier NULL,
        [ModifiedOnUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_OperatingFinancialPlans] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_OperatingFinancialPlans_DoctorId_Id] UNIQUE ([DoctorId], [Id]),
        CONSTRAINT [CK_OperatingPlan_Dates] CHECK ([EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]),
        CONSTRAINT [CK_OperatingPlan_Kind] CHECK ([Kind] IN (1,2)),
        CONSTRAINT [CK_OperatingPlan_Reception] CHECK (([Kind] = 1 AND [ReceptionId] IS NOT NULL) OR ([Kind] = 2 AND [ReceptionId] IS NULL)),
        CONSTRAINT [CK_OperatingPlan_Status] CHECK ([Status] IN (1,2,3)),
        CONSTRAINT [FK_OperatingFinancialPlans_ApplicationUsers_CreatedByApplicationUserId] FOREIGN KEY ([CreatedByApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OperatingFinancialPlans_ApplicationUsers_ModifiedByApplicationUserId] FOREIGN KEY ([ModifiedByApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OperatingFinancialPlans_Doctors_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [Doctors] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OperatingFinancialPlans_Receptions_DoctorId_ReceptionId] FOREIGN KEY ([DoctorId], [ReceptionId]) REFERENCES [Receptions] ([OwnerDoctorId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE TABLE [OperatingGenerationRuns] (
        [Id] uniqueidentifier NOT NULL,
        [StartedAtUtc] datetime2 NOT NULL,
        [FinishedAtUtc] datetime2 NULL,
        [Status] nvarchar(20) NOT NULL,
        [GeneratedCount] int NOT NULL,
        [FailureCode] nvarchar(100) NULL,
        CONSTRAINT [PK_OperatingGenerationRuns] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE TABLE [OperatingPlanRevisions] (
        [Id] uniqueidentifier NOT NULL,
        [PlanId] uniqueidentifier NOT NULL,
        [DoctorId] uniqueidentifier NOT NULL,
        [RevisionNumber] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [CurrencyCode] nvarchar(3) NOT NULL,
        [DueDay] int NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        [Cadence] int NOT NULL,
        [AmountPolicy] int NOT NULL,
        [Category] int NOT NULL,
        [DescriptionAr] nvarchar(1000) NOT NULL,
        [DescriptionEn] nvarchar(1000) NULL,
        [Status] int NOT NULL,
        [IsLifecycleChange] bit NOT NULL,
        [ActorApplicationUserId] uniqueidentifier NOT NULL,
        [RecordedAtUtc] datetime2 NOT NULL,
        [Reason] nvarchar(2000) NOT NULL,
        CONSTRAINT [PK_OperatingPlanRevisions] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_OperatingPlanRevisions_DoctorId_Id] UNIQUE ([DoctorId], [Id]),
        CONSTRAINT [CK_OperatingPlanRevision_Amount] CHECK (CAST([Amount] AS decimal(18,2)) > 0),
        CONSTRAINT [CK_OperatingPlanRevision_Cadence] CHECK ([Cadence] IN (1,3,12)),
        CONSTRAINT [CK_OperatingPlanRevision_Currency] CHECK ([CurrencyCode] = 'EGP'),
        CONSTRAINT [CK_OperatingPlanRevision_Day] CHECK ([DueDay] BETWEEN 1 AND 31),
        CONSTRAINT [CK_OperatingPlanRevision_Policy] CHECK ([AmountPolicy] IN (1,2)),
        CONSTRAINT [FK_OperatingPlanRevisions_ApplicationUsers_ActorApplicationUserId] FOREIGN KEY ([ActorApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OperatingPlanRevisions_OperatingFinancialPlans_DoctorId_PlanId] FOREIGN KEY ([DoctorId], [PlanId]) REFERENCES [OperatingFinancialPlans] ([DoctorId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE TABLE [OperatingPlanAllocations] (
        [Id] uniqueidentifier NOT NULL,
        [RevisionId] uniqueidentifier NOT NULL,
        [DoctorId] uniqueidentifier NOT NULL,
        [PracticeId] uniqueidentifier NOT NULL,
        [Percentage] decimal(9,4) NOT NULL,
        CONSTRAINT [PK_OperatingPlanAllocations] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_OperatingPlanAllocation_Percent] CHECK (CAST([Percentage] AS decimal(9,4)) > 0 AND CAST([Percentage] AS decimal(9,4)) <= 100),
        CONSTRAINT [FK_OperatingPlanAllocations_DoctorPractices_DoctorId_PracticeId] FOREIGN KEY ([DoctorId], [PracticeId]) REFERENCES [DoctorPractices] ([DoctorId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OperatingPlanAllocations_OperatingPlanRevisions_DoctorId_RevisionId] FOREIGN KEY ([DoctorId], [RevisionId]) REFERENCES [OperatingPlanRevisions] ([DoctorId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE TABLE [OperatingCostAllocationSnapshots] (
        [Id] uniqueidentifier NOT NULL,
        [RevisionId] uniqueidentifier NOT NULL,
        [DoctorId] uniqueidentifier NOT NULL,
        [PracticeId] uniqueidentifier NOT NULL,
        [Percentage] decimal(9,4) NOT NULL,
        [AmountMinorUnits] bigint NOT NULL,
        CONSTRAINT [PK_OperatingCostAllocationSnapshots] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_OperatingCostAllocation_Amount] CHECK ([AmountMinorUnits] >= 0),
        CONSTRAINT [CK_OperatingCostAllocation_Percent] CHECK (CAST([Percentage] AS decimal(9,4)) > 0 AND CAST([Percentage] AS decimal(9,4)) <= 100),
        CONSTRAINT [FK_OperatingCostAllocationSnapshots_DoctorPractices_DoctorId_PracticeId] FOREIGN KEY ([DoctorId], [PracticeId]) REFERENCES [DoctorPractices] ([DoctorId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE TABLE [OperatingObligationRevisions] (
        [Id] uniqueidentifier NOT NULL,
        [ObligationId] uniqueidentifier NOT NULL,
        [DoctorId] uniqueidentifier NOT NULL,
        [RevisionNumber] int NOT NULL,
        [AmountMinorUnits] bigint NOT NULL,
        [DueBusinessDate] date NOT NULL,
        [Category] int NOT NULL,
        [DescriptionAr] nvarchar(1000) NOT NULL,
        [DescriptionEn] nvarchar(1000) NULL,
        [ActualAmountConfirmed] bit NOT NULL,
        [Status] int NOT NULL,
        [Reason] nvarchar(2000) NOT NULL,
        [ActorApplicationUserId] uniqueidentifier NOT NULL,
        [RecordedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_OperatingObligationRevisions] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_OperatingObligationRevisions_DoctorId_Id] UNIQUE ([DoctorId], [Id]),
        CONSTRAINT [AK_OperatingObligationRevisions_ObligationId_Id] UNIQUE ([ObligationId], [Id]),
        CONSTRAINT [CK_OperatingObligationRevision_Amount] CHECK ([AmountMinorUnits] > 0),
        CONSTRAINT [FK_OperatingObligationRevisions_ApplicationUsers_ActorApplicationUserId] FOREIGN KEY ([ActorApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE TABLE [OperatingObligations] (
        [Id] uniqueidentifier NOT NULL,
        [DoctorId] uniqueidentifier NOT NULL,
        [PlanId] uniqueidentifier NULL,
        [ReceptionId] uniqueidentifier NULL,
        [Kind] int NOT NULL,
        [OccurrenceKey] nvarchar(16) NULL,
        [PeriodYear] int NULL,
        [PeriodMonth] int NULL,
        [BusinessDate] date NOT NULL,
        [DueBusinessDate] date NOT NULL,
        [PlannedAmountMinorUnits] bigint NOT NULL,
        [AmountMinorUnits] bigint NOT NULL,
        [CurrencyCode] nvarchar(3) NOT NULL,
        [AmountPolicy] int NOT NULL,
        [ActualAmountConfirmed] bit NOT NULL,
        [Category] int NOT NULL,
        [DescriptionAr] nvarchar(1000) NOT NULL,
        [DescriptionEn] nvarchar(1000) NULL,
        [Status] int NOT NULL,
        [ActiveSettlementId] uniqueidentifier NULL,
        [CurrentRevisionNumber] int NOT NULL,
        [CreatedByApplicationUserId] uniqueidentifier NOT NULL,
        [RecordedAtUtc] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_OperatingObligations] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_OperatingObligations_DoctorId_Id] UNIQUE ([DoctorId], [Id]),
        CONSTRAINT [CK_OperatingObligation_Amount] CHECK ([AmountMinorUnits] > 0 AND [PlannedAmountMinorUnits] > 0),
        CONSTRAINT [CK_OperatingObligation_Currency] CHECK ([CurrencyCode] = 'EGP'),
        CONSTRAINT [CK_OperatingObligation_Occurrence] CHECK (([Kind] = 2 AND [PlanId] IS NULL AND [OccurrenceKey] IS NULL) OR ([Kind] IN (1,3) AND [PlanId] IS NOT NULL AND [OccurrenceKey] IS NOT NULL AND [PeriodMonth] BETWEEN 1 AND 12 AND [PeriodYear] BETWEEN 1 AND 9999)),
        CONSTRAINT [CK_OperatingObligation_State] CHECK (([Status] = 2 AND [ActiveSettlementId] IS NOT NULL) OR ([Status] IN (1,3) AND [ActiveSettlementId] IS NULL)),
        CONSTRAINT [FK_OperatingObligations_ApplicationUsers_CreatedByApplicationUserId] FOREIGN KEY ([CreatedByApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OperatingObligations_Doctors_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [Doctors] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OperatingObligations_OperatingFinancialPlans_DoctorId_PlanId] FOREIGN KEY ([DoctorId], [PlanId]) REFERENCES [OperatingFinancialPlans] ([DoctorId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OperatingObligations_Receptions_DoctorId_ReceptionId] FOREIGN KEY ([DoctorId], [ReceptionId]) REFERENCES [Receptions] ([OwnerDoctorId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE TABLE [OperatingSettlements] (
        [Id] uniqueidentifier NOT NULL,
        [DoctorId] uniqueidentifier NOT NULL,
        [ObligationId] uniqueidentifier NOT NULL,
        [ObligationRevisionId] uniqueidentifier NOT NULL,
        [ReceiptNumber] nvarchar(40) NOT NULL,
        [AmountMinorUnits] bigint NOT NULL,
        [CurrencyCode] nvarchar(3) NOT NULL,
        [PaymentMethod] int NOT NULL,
        [Reference] nvarchar(200) NULL,
        [BusinessDate] date NOT NULL,
        [ReportTimeZoneId] nvarchar(100) NOT NULL,
        [BackdateReason] nvarchar(2000) NULL,
        [ActorApplicationUserId] uniqueidentifier NOT NULL,
        [RecordedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_OperatingSettlements] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_OperatingSettlements_ObligationId_Id] UNIQUE ([ObligationId], [Id]),
        CONSTRAINT [CK_OperatingSettlement_Amount] CHECK ([AmountMinorUnits] > 0),
        CONSTRAINT [CK_OperatingSettlement_Currency] CHECK ([CurrencyCode] = 'EGP'),
        CONSTRAINT [CK_OperatingSettlement_Method] CHECK ([PaymentMethod] IN (1,2,3,4)),
        CONSTRAINT [FK_OperatingSettlements_ApplicationUsers_ActorApplicationUserId] FOREIGN KEY ([ActorApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OperatingSettlements_OperatingObligationRevisions_ObligationId_ObligationRevisionId] FOREIGN KEY ([ObligationId], [ObligationRevisionId]) REFERENCES [OperatingObligationRevisions] ([ObligationId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OperatingSettlements_OperatingObligations_DoctorId_ObligationId] FOREIGN KEY ([DoctorId], [ObligationId]) REFERENCES [OperatingObligations] ([DoctorId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE TABLE [OperatingSettlementReversals] (
        [Id] uniqueidentifier NOT NULL,
        [DoctorId] uniqueidentifier NOT NULL,
        [OriginalSettlementId] uniqueidentifier NOT NULL,
        [ObligationId] uniqueidentifier NOT NULL,
        [AmountMinorUnits] bigint NOT NULL,
        [BusinessDate] date NOT NULL,
        [ReportTimeZoneId] nvarchar(100) NOT NULL,
        [Reason] nvarchar(2000) NOT NULL,
        [ActorApplicationUserId] uniqueidentifier NOT NULL,
        [RecordedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_OperatingSettlementReversals] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_OperatingReversal_Amount] CHECK ([AmountMinorUnits] > 0),
        CONSTRAINT [CK_OperatingReversal_Reason] CHECK (NULLIF(LTRIM(RTRIM([Reason])), '') IS NOT NULL),
        CONSTRAINT [FK_OperatingSettlementReversals_ApplicationUsers_ActorApplicationUserId] FOREIGN KEY ([ActorApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OperatingSettlementReversals_OperatingObligations_DoctorId_ObligationId] FOREIGN KEY ([DoctorId], [ObligationId]) REFERENCES [OperatingObligations] ([DoctorId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OperatingSettlementReversals_OperatingSettlements_ObligationId_OriginalSettlementId] FOREIGN KEY ([ObligationId], [OriginalSettlementId]) REFERENCES [OperatingSettlements] ([ObligationId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingCostAllocationSnapshots_DoctorId_PracticeId] ON [OperatingCostAllocationSnapshots] ([DoctorId], [PracticeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingCostAllocationSnapshots_DoctorId_RevisionId] ON [OperatingCostAllocationSnapshots] ([DoctorId], [RevisionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingCostAllocationSnapshots_PracticeId_RevisionId] ON [OperatingCostAllocationSnapshots] ([PracticeId], [RevisionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OperatingCostAllocationSnapshots_RevisionId_PracticeId] ON [OperatingCostAllocationSnapshots] ([RevisionId], [PracticeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingFinanceHistories_ActorApplicationUserId] ON [OperatingFinanceHistories] ([ActorApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingFinanceHistories_DoctorId_ResourceType_ResourceId_RecordedAtUtc] ON [OperatingFinanceHistories] ([DoctorId], [ResourceType], [ResourceId], [RecordedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingFinancialPlans_CreatedByApplicationUserId] ON [OperatingFinancialPlans] ([CreatedByApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingFinancialPlans_DoctorId_Kind_Status_EffectiveFrom] ON [OperatingFinancialPlans] ([DoctorId], [Kind], [Status], [EffectiveFrom]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingFinancialPlans_DoctorId_ReceptionId_EffectiveFrom_EffectiveTo] ON [OperatingFinancialPlans] ([DoctorId], [ReceptionId], [EffectiveFrom], [EffectiveTo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingFinancialPlans_ModifiedByApplicationUserId] ON [OperatingFinancialPlans] ([ModifiedByApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingGenerationRuns_StartedAtUtc] ON [OperatingGenerationRuns] ([StartedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingObligationRevisions_ActorApplicationUserId] ON [OperatingObligationRevisions] ([ActorApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingObligationRevisions_DoctorId_ObligationId] ON [OperatingObligationRevisions] ([DoctorId], [ObligationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingObligationRevisions_DoctorId_RecordedAtUtc_DueBusinessDate] ON [OperatingObligationRevisions] ([DoctorId], [RecordedAtUtc], [DueBusinessDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OperatingObligationRevisions_ObligationId_RevisionNumber] ON [OperatingObligationRevisions] ([ObligationId], [RevisionNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OperatingObligations_ActiveSettlementId] ON [OperatingObligations] ([ActiveSettlementId]) WHERE [ActiveSettlementId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingObligations_CreatedByApplicationUserId] ON [OperatingObligations] ([CreatedByApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingObligations_DoctorId_PlanId] ON [OperatingObligations] ([DoctorId], [PlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingObligations_DoctorId_ReceptionId] ON [OperatingObligations] ([DoctorId], [ReceptionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingObligations_DoctorId_Status_DueBusinessDate] ON [OperatingObligations] ([DoctorId], [Status], [DueBusinessDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingObligations_Id_ActiveSettlementId] ON [OperatingObligations] ([Id], [ActiveSettlementId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OperatingObligations_PlanId_OccurrenceKey] ON [OperatingObligations] ([PlanId], [OccurrenceKey]) WHERE [PlanId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OperatingObligations_PlanId_PeriodYear_PeriodMonth] ON [OperatingObligations] ([PlanId], [PeriodYear], [PeriodMonth]) WHERE [PlanId] IS NOT NULL AND [Kind] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingPlanAllocations_DoctorId_PracticeId] ON [OperatingPlanAllocations] ([DoctorId], [PracticeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingPlanAllocations_DoctorId_RevisionId] ON [OperatingPlanAllocations] ([DoctorId], [RevisionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OperatingPlanAllocations_RevisionId_PracticeId] ON [OperatingPlanAllocations] ([RevisionId], [PracticeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingPlanRevisions_ActorApplicationUserId] ON [OperatingPlanRevisions] ([ActorApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingPlanRevisions_DoctorId_PlanId] ON [OperatingPlanRevisions] ([DoctorId], [PlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingPlanRevisions_PlanId_EffectiveFrom_RecordedAtUtc] ON [OperatingPlanRevisions] ([PlanId], [EffectiveFrom], [RecordedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OperatingPlanRevisions_PlanId_RevisionNumber] ON [OperatingPlanRevisions] ([PlanId], [RevisionNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingSettlementReversals_ActorApplicationUserId] ON [OperatingSettlementReversals] ([ActorApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingSettlementReversals_DoctorId_BusinessDate_RecordedAtUtc] ON [OperatingSettlementReversals] ([DoctorId], [BusinessDate], [RecordedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingSettlementReversals_DoctorId_ObligationId] ON [OperatingSettlementReversals] ([DoctorId], [ObligationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingSettlementReversals_ObligationId_OriginalSettlementId] ON [OperatingSettlementReversals] ([ObligationId], [OriginalSettlementId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OperatingSettlementReversals_OriginalSettlementId] ON [OperatingSettlementReversals] ([OriginalSettlementId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingSettlements_ActorApplicationUserId] ON [OperatingSettlements] ([ActorApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingSettlements_DoctorId_BusinessDate_RecordedAtUtc] ON [OperatingSettlements] ([DoctorId], [BusinessDate], [RecordedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingSettlements_DoctorId_ObligationId] ON [OperatingSettlements] ([DoctorId], [ObligationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE INDEX [IX_OperatingSettlements_ObligationId_ObligationRevisionId] ON [OperatingSettlements] ([ObligationId], [ObligationRevisionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OperatingSettlements_ReceiptNumber] ON [OperatingSettlements] ([ReceiptNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    ALTER TABLE [OperatingCostAllocationSnapshots] ADD CONSTRAINT [FK_OperatingCostAllocationSnapshots_OperatingObligationRevisions_DoctorId_RevisionId] FOREIGN KEY ([DoctorId], [RevisionId]) REFERENCES [OperatingObligationRevisions] ([DoctorId], [Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    ALTER TABLE [OperatingObligationRevisions] ADD CONSTRAINT [FK_OperatingObligationRevisions_OperatingObligations_DoctorId_ObligationId] FOREIGN KEY ([DoctorId], [ObligationId]) REFERENCES [OperatingObligations] ([DoctorId], [Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    ALTER TABLE [OperatingObligations] ADD CONSTRAINT [FK_OperatingObligations_OperatingSettlements_Id_ActiveSettlementId] FOREIGN KEY ([Id], [ActiveSettlementId]) REFERENCES [OperatingSettlements] ([ObligationId], [Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE TRIGGER [TR_OperatingPlanRevisions_Immutable] ON [OperatingPlanRevisions] INSTEAD OF UPDATE, DELETE AS BEGIN THROW 51018, 'Operating financial history is immutable.', 1; END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE TRIGGER [TR_OperatingPlanAllocations_Immutable] ON [OperatingPlanAllocations] INSTEAD OF UPDATE, DELETE AS BEGIN THROW 51018, 'Operating financial history is immutable.', 1; END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE TRIGGER [TR_OperatingObligationRevisions_Immutable] ON [OperatingObligationRevisions] INSTEAD OF UPDATE, DELETE AS BEGIN THROW 51018, 'Operating financial history is immutable.', 1; END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE TRIGGER [TR_OperatingCostAllocationSnapshots_Immutable] ON [OperatingCostAllocationSnapshots] INSTEAD OF UPDATE, DELETE AS BEGIN THROW 51018, 'Operating financial history is immutable.', 1; END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE TRIGGER [TR_OperatingSettlements_Immutable] ON [OperatingSettlements] INSTEAD OF UPDATE, DELETE AS BEGIN THROW 51018, 'Operating financial history is immutable.', 1; END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE TRIGGER [TR_OperatingSettlementReversals_Immutable] ON [OperatingSettlementReversals] INSTEAD OF UPDATE, DELETE AS BEGIN THROW 51018, 'Operating financial history is immutable.', 1; END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE TRIGGER [TR_OperatingFinanceHistories_Immutable] ON [OperatingFinanceHistories] INSTEAD OF UPDATE, DELETE AS BEGIN THROW 51018, 'Operating financial history is immutable.', 1; END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE TRIGGER [TR_OperatingSettlements_FullAmount] ON [OperatingSettlements] AFTER INSERT AS
    BEGIN
        IF EXISTS (SELECT 1 FROM inserted i JOIN OperatingObligationRevisions r ON r.Id = i.ObligationRevisionId
            WHERE i.AmountMinorUnits <> r.AmountMinorUnits OR r.ActualAmountConfirmed = 0)
            THROW 51018, 'Full settlement of a confirmed obligation is required.', 1;
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    CREATE TRIGGER [TR_OperatingReversals_FullAmount] ON [OperatingSettlementReversals] AFTER INSERT AS
    BEGIN
        IF EXISTS (SELECT 1 FROM inserted i JOIN OperatingSettlements s ON s.Id = i.OriginalSettlementId
            WHERE i.AmountMinorUnits <> s.AmountMinorUnits OR i.BusinessDate < s.BusinessDate)
            THROW 51018, 'A full compensating reversal is required.', 1;
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013113259_Phase18OperatingFinance'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013113259_Phase18OperatingFinance', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013133419_Phase18ReportsAndInsights'
)
BEGIN
    ALTER TABLE [Reservations] ADD [CheckInGracePeriodMinutesSnapshot] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013133419_Phase18ReportsAndInsights'
)
BEGIN
    ALTER TABLE [OperatingObligations] ADD [GeneratedBySystem] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013133419_Phase18ReportsAndInsights'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OperatingFinanceHistories]') AND [c].[name] = N'ActorApplicationUserId');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [OperatingFinanceHistories] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [OperatingFinanceHistories] ALTER COLUMN [ActorApplicationUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013133419_Phase18ReportsAndInsights'
)
BEGIN
    ALTER TABLE [OperatingFinanceHistories] ADD [SnapshotRevisionId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013133419_Phase18ReportsAndInsights'
)
BEGIN
    CREATE TABLE [DoctorInsightFollowUps] (
        [Id] uniqueidentifier NOT NULL,
        [DoctorId] uniqueidentifier NOT NULL,
        [PracticeId] uniqueidentifier NOT NULL,
        [MetricCode] nvarchar(80) NOT NULL,
        [EvidenceJson] nvarchar(max) NOT NULL,
        [State] int NOT NULL,
        [CreatedByApplicationUserId] uniqueidentifier NOT NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [ModifiedOnUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_DoctorInsightFollowUps] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_DoctorInsight_State] CHECK ([State] IN (1,2,3,4)),
        CONSTRAINT [FK_DoctorInsightFollowUps_ApplicationUsers_CreatedByApplicationUserId] FOREIGN KEY ([CreatedByApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DoctorInsightFollowUps_DoctorPractices_DoctorId_PracticeId] FOREIGN KEY ([DoctorId], [PracticeId]) REFERENCES [DoctorPractices] ([DoctorId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013133419_Phase18ReportsAndInsights'
)
BEGIN
    CREATE TABLE [PracticeReportingPolicySnapshots] (
        [Id] uniqueidentifier NOT NULL,
        [DoctorId] uniqueidentifier NOT NULL,
        [PracticeId] uniqueidentifier NOT NULL,
        [RevisionNumber] bigint NOT NULL,
        [PolicyJson] nvarchar(max) NOT NULL,
        [RecordedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PracticeReportingPolicySnapshots] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PracticeReportingPolicySnapshots_DoctorPractices_DoctorId_PracticeId] FOREIGN KEY ([DoctorId], [PracticeId]) REFERENCES [DoctorPractices] ([DoctorId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013133419_Phase18ReportsAndInsights'
)
BEGIN
    CREATE TABLE [DoctorInsightHistories] (
        [Id] uniqueidentifier NOT NULL,
        [InsightId] uniqueidentifier NOT NULL,
        [Operation] nvarchar(80) NOT NULL,
        [State] int NOT NULL,
        [ActorApplicationUserId] uniqueidentifier NOT NULL,
        [RecordedAtUtc] datetime2 NOT NULL,
        [Reason] nvarchar(2000) NULL,
        [SequenceNumber] int NOT NULL,
        CONSTRAINT [PK_DoctorInsightHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DoctorInsightHistories_ApplicationUsers_ActorApplicationUserId] FOREIGN KEY ([ActorApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DoctorInsightHistories_DoctorInsightFollowUps_InsightId] FOREIGN KEY ([InsightId]) REFERENCES [DoctorInsightFollowUps] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013133419_Phase18ReportsAndInsights'
)
BEGIN
    CREATE INDEX [IX_DoctorInsightFollowUps_CreatedByApplicationUserId] ON [DoctorInsightFollowUps] ([CreatedByApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013133419_Phase18ReportsAndInsights'
)
BEGIN
    CREATE INDEX [IX_DoctorInsightFollowUps_DoctorId_PracticeId_State_CreatedOnUtc] ON [DoctorInsightFollowUps] ([DoctorId], [PracticeId], [State], [CreatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013133419_Phase18ReportsAndInsights'
)
BEGIN
    CREATE INDEX [IX_DoctorInsightHistories_ActorApplicationUserId] ON [DoctorInsightHistories] ([ActorApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013133419_Phase18ReportsAndInsights'
)
BEGIN
    CREATE INDEX [IX_DoctorInsightHistories_InsightId_RecordedAtUtc] ON [DoctorInsightHistories] ([InsightId], [RecordedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013133419_Phase18ReportsAndInsights'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DoctorInsightHistories_InsightId_SequenceNumber] ON [DoctorInsightHistories] ([InsightId], [SequenceNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013133419_Phase18ReportsAndInsights'
)
BEGIN
    CREATE INDEX [IX_PracticeReportingPolicySnapshots_DoctorId_PracticeId] ON [PracticeReportingPolicySnapshots] ([DoctorId], [PracticeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013133419_Phase18ReportsAndInsights'
)
BEGIN
    CREATE INDEX [IX_PracticeReportingPolicySnapshots_PracticeId_RecordedAtUtc] ON [PracticeReportingPolicySnapshots] ([PracticeId], [RecordedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013133419_Phase18ReportsAndInsights'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PracticeReportingPolicySnapshots_PracticeId_RevisionNumber] ON [PracticeReportingPolicySnapshots] ([PracticeId], [RevisionNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013133419_Phase18ReportsAndInsights'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013133419_Phase18ReportsAndInsights', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013135730_Phase18ReceptionHandovers'
)
BEGIN
    ALTER TABLE [Tickets] ADD CONSTRAINT [AK_Tickets_DoctorPracticeId_Id] UNIQUE ([DoctorPracticeId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013135730_Phase18ReceptionHandovers'
)
BEGIN
    ALTER TABLE [Reservations] ADD CONSTRAINT [AK_Reservations_DoctorPracticeId_Id] UNIQUE ([DoctorPracticeId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013135730_Phase18ReceptionHandovers'
)
BEGIN
    CREATE TABLE [ReceptionHandovers] (
        [Id] uniqueidentifier NOT NULL,
        [DoctorId] uniqueidentifier NOT NULL,
        [DoctorPracticeId] uniqueidentifier NOT NULL,
        [ReservationId] uniqueidentifier NULL,
        [TicketId] uniqueidentifier NULL,
        [PreviousHandoverId] uniqueidentifier NULL,
        [Summary] nvarchar(max) NOT NULL,
        [Status] int NOT NULL,
        [CreatedByApplicationUserId] uniqueidentifier NOT NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [AcknowledgedByApplicationUserId] uniqueidentifier NULL,
        [AcknowledgedOnUtc] datetime2 NULL,
        [ClosedByApplicationUserId] uniqueidentifier NULL,
        [ClosedOnUtc] datetime2 NULL,
        [ClosureOutcome] nvarchar(max) NULL,
        [HistorySequence] int NOT NULL,
        [ModifiedOnUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ReceptionHandovers] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_ReceptionHandovers_DoctorPracticeId_Id] UNIQUE ([DoctorPracticeId], [Id]),
        CONSTRAINT [CK_Handover_Status] CHECK ([Status] IN (1,2,3)),
        CONSTRAINT [FK_ReceptionHandovers_ApplicationUsers_AcknowledgedByApplicationUserId] FOREIGN KEY ([AcknowledgedByApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReceptionHandovers_ApplicationUsers_ClosedByApplicationUserId] FOREIGN KEY ([ClosedByApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReceptionHandovers_ApplicationUsers_CreatedByApplicationUserId] FOREIGN KEY ([CreatedByApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReceptionHandovers_DoctorPractices_DoctorId_DoctorPracticeId] FOREIGN KEY ([DoctorId], [DoctorPracticeId]) REFERENCES [DoctorPractices] ([DoctorId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReceptionHandovers_ReceptionHandovers_DoctorPracticeId_PreviousHandoverId] FOREIGN KEY ([DoctorPracticeId], [PreviousHandoverId]) REFERENCES [ReceptionHandovers] ([DoctorPracticeId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReceptionHandovers_Reservations_DoctorPracticeId_ReservationId] FOREIGN KEY ([DoctorPracticeId], [ReservationId]) REFERENCES [Reservations] ([DoctorPracticeId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReceptionHandovers_Tickets_DoctorPracticeId_TicketId] FOREIGN KEY ([DoctorPracticeId], [TicketId]) REFERENCES [Tickets] ([DoctorPracticeId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013135730_Phase18ReceptionHandovers'
)
BEGIN
    CREATE TABLE [ReceptionHandoverHistories] (
        [Id] uniqueidentifier NOT NULL,
        [HandoverId] uniqueidentifier NOT NULL,
        [Operation] nvarchar(50) NOT NULL,
        [Status] int NOT NULL,
        [ActorApplicationUserId] uniqueidentifier NOT NULL,
        [RecordedAtUtc] datetime2 NOT NULL,
        [Note] nvarchar(max) NULL,
        [SequenceNumber] int NOT NULL,
        CONSTRAINT [PK_ReceptionHandoverHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReceptionHandoverHistories_ApplicationUsers_ActorApplicationUserId] FOREIGN KEY ([ActorApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReceptionHandoverHistories_ReceptionHandovers_HandoverId] FOREIGN KEY ([HandoverId]) REFERENCES [ReceptionHandovers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013135730_Phase18ReceptionHandovers'
)
BEGIN
    CREATE INDEX [IX_ReceptionHandoverHistories_ActorApplicationUserId] ON [ReceptionHandoverHistories] ([ActorApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013135730_Phase18ReceptionHandovers'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReceptionHandoverHistories_HandoverId_SequenceNumber] ON [ReceptionHandoverHistories] ([HandoverId], [SequenceNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013135730_Phase18ReceptionHandovers'
)
BEGIN
    CREATE INDEX [IX_ReceptionHandovers_AcknowledgedByApplicationUserId] ON [ReceptionHandovers] ([AcknowledgedByApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013135730_Phase18ReceptionHandovers'
)
BEGIN
    CREATE INDEX [IX_ReceptionHandovers_ClosedByApplicationUserId] ON [ReceptionHandovers] ([ClosedByApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013135730_Phase18ReceptionHandovers'
)
BEGIN
    CREATE INDEX [IX_ReceptionHandovers_CreatedByApplicationUserId] ON [ReceptionHandovers] ([CreatedByApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013135730_Phase18ReceptionHandovers'
)
BEGIN
    CREATE INDEX [IX_ReceptionHandovers_DoctorId_DoctorPracticeId] ON [ReceptionHandovers] ([DoctorId], [DoctorPracticeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013135730_Phase18ReceptionHandovers'
)
BEGIN
    CREATE INDEX [IX_ReceptionHandovers_DoctorPracticeId_PreviousHandoverId] ON [ReceptionHandovers] ([DoctorPracticeId], [PreviousHandoverId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013135730_Phase18ReceptionHandovers'
)
BEGIN
    CREATE INDEX [IX_ReceptionHandovers_DoctorPracticeId_ReservationId] ON [ReceptionHandovers] ([DoctorPracticeId], [ReservationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013135730_Phase18ReceptionHandovers'
)
BEGIN
    CREATE INDEX [IX_ReceptionHandovers_DoctorPracticeId_Status_CreatedOnUtc] ON [ReceptionHandovers] ([DoctorPracticeId], [Status], [CreatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013135730_Phase18ReceptionHandovers'
)
BEGIN
    CREATE INDEX [IX_ReceptionHandovers_DoctorPracticeId_TicketId] ON [ReceptionHandovers] ([DoctorPracticeId], [TicketId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013135730_Phase18ReceptionHandovers'
)
BEGIN
    CREATE TRIGGER [TR_Immutable_ReceptionHandoverHistories] ON [ReceptionHandoverHistories] AFTER UPDATE, DELETE AS THROW 51018, 'Handover history is immutable.', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013135730_Phase18ReceptionHandovers'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013135730_Phase18ReceptionHandovers', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013140941_Phase18AdministrativeInvestigations'
)
BEGIN
    CREATE TABLE [AdministrativeInvestigations] (
        [Id] uniqueidentifier NOT NULL,
        [TitleAr] nvarchar(1200) NOT NULL,
        [TitleEn] nvarchar(1200) NULL,
        [Description] nvarchar(max) NOT NULL,
        [IssueType] int NOT NULL,
        [Priority] int NOT NULL,
        [LinkedResourceType] int NOT NULL,
        [LinkedResourceId] uniqueidentifier NULL,
        [LinkedDoctorId] uniqueidentifier NULL,
        [LinkedSpecializationRequestId] uniqueidentifier NULL,
        [LinkedBackgroundJobRunId] uniqueidentifier NULL,
        [LinkedOutboxMessageId] uniqueidentifier NULL,
        [CreatedByApplicationUserId] uniqueidentifier NOT NULL,
        [AssignedSuperAdminId] uniqueidentifier NULL,
        [Status] int NOT NULL,
        [ResolutionType] int NULL,
        [ResolutionNotes] nvarchar(max) NULL,
        [ClosureNotes] nvarchar(max) NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [ModifiedOnUtc] datetime2 NOT NULL,
        [ResolvedOnUtc] datetime2 NULL,
        [ClosedOnUtc] datetime2 NULL,
        [HistorySequence] int NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_AdministrativeInvestigations] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Investigation_Assignee] CHECK ([Status] <> 2 OR [AssignedSuperAdminId] IS NOT NULL),
        CONSTRAINT [CK_Investigation_Closure] CHECK ([Status] <> 4 OR ([ClosureNotes] IS NOT NULL AND [ClosedOnUtc] IS NOT NULL)),
        CONSTRAINT [CK_Investigation_Link] CHECK (([LinkedResourceType] = 0 AND [LinkedResourceId] IS NULL AND [LinkedDoctorId] IS NULL AND [LinkedSpecializationRequestId] IS NULL AND [LinkedBackgroundJobRunId] IS NULL AND [LinkedOutboxMessageId] IS NULL) OR ([LinkedResourceType] = 1 AND [LinkedResourceId] = [LinkedDoctorId] AND [LinkedDoctorId] IS NOT NULL AND [LinkedSpecializationRequestId] IS NULL AND [LinkedBackgroundJobRunId] IS NULL AND [LinkedOutboxMessageId] IS NULL) OR ([LinkedResourceType] = 2 AND [LinkedResourceId] = [LinkedSpecializationRequestId] AND [LinkedSpecializationRequestId] IS NOT NULL AND [LinkedDoctorId] IS NULL AND [LinkedBackgroundJobRunId] IS NULL AND [LinkedOutboxMessageId] IS NULL) OR ([LinkedResourceType] = 3 AND [LinkedResourceId] = [LinkedBackgroundJobRunId] AND [LinkedBackgroundJobRunId] IS NOT NULL AND [LinkedDoctorId] IS NULL AND [LinkedSpecializationRequestId] IS NULL AND [LinkedOutboxMessageId] IS NULL) OR ([LinkedResourceType] = 4 AND [LinkedResourceId] = [LinkedOutboxMessageId] AND [LinkedOutboxMessageId] IS NOT NULL AND [LinkedDoctorId] IS NULL AND [LinkedSpecializationRequestId] IS NULL AND [LinkedBackgroundJobRunId] IS NULL)),
        CONSTRAINT [CK_Investigation_Resolution] CHECK ([Status] <> 3 OR ([ResolutionType] IS NOT NULL AND [ResolutionNotes] IS NOT NULL AND [ResolvedOnUtc] IS NOT NULL)),
        CONSTRAINT [CK_Investigation_State] CHECK ([Status] IN (1,2,3,4) AND [Priority] IN (1,2) AND [IssueType] BETWEEN 1 AND 7),
        CONSTRAINT [FK_AdministrativeInvestigations_ApplicationUsers_CreatedByApplicationUserId] FOREIGN KEY ([CreatedByApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AdministrativeInvestigations_DoctorSpecializationRequests_LinkedSpecializationRequestId] FOREIGN KEY ([LinkedSpecializationRequestId]) REFERENCES [DoctorSpecializationRequests] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AdministrativeInvestigations_Doctors_LinkedDoctorId] FOREIGN KEY ([LinkedDoctorId]) REFERENCES [Doctors] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AdministrativeInvestigations_EmailOutboxMessages_LinkedOutboxMessageId] FOREIGN KEY ([LinkedOutboxMessageId]) REFERENCES [EmailOutboxMessages] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AdministrativeInvestigations_OperatingGenerationRuns_LinkedBackgroundJobRunId] FOREIGN KEY ([LinkedBackgroundJobRunId]) REFERENCES [OperatingGenerationRuns] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AdministrativeInvestigations_SuperAdmins_AssignedSuperAdminId] FOREIGN KEY ([AssignedSuperAdminId]) REFERENCES [SuperAdmins] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013140941_Phase18AdministrativeInvestigations'
)
BEGIN
    CREATE TABLE [InvestigationHistories] (
        [Id] uniqueidentifier NOT NULL,
        [CaseId] uniqueidentifier NOT NULL,
        [Operation] nvarchar(50) NOT NULL,
        [Status] int NOT NULL,
        [ActorApplicationUserId] uniqueidentifier NOT NULL,
        [AssignedSuperAdminId] uniqueidentifier NULL,
        [ResolutionType] int NULL,
        [Note] nvarchar(max) NULL,
        [RecordedAtUtc] datetime2 NOT NULL,
        [SequenceNumber] int NOT NULL,
        CONSTRAINT [PK_InvestigationHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InvestigationHistories_AdministrativeInvestigations_CaseId] FOREIGN KEY ([CaseId]) REFERENCES [AdministrativeInvestigations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InvestigationHistories_ApplicationUsers_ActorApplicationUserId] FOREIGN KEY ([ActorApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InvestigationHistories_SuperAdmins_AssignedSuperAdminId] FOREIGN KEY ([AssignedSuperAdminId]) REFERENCES [SuperAdmins] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013140941_Phase18AdministrativeInvestigations'
)
BEGIN
    CREATE INDEX [IX_AdministrativeInvestigations_AssignedSuperAdminId_Status] ON [AdministrativeInvestigations] ([AssignedSuperAdminId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013140941_Phase18AdministrativeInvestigations'
)
BEGIN
    CREATE INDEX [IX_AdministrativeInvestigations_CreatedByApplicationUserId] ON [AdministrativeInvestigations] ([CreatedByApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013140941_Phase18AdministrativeInvestigations'
)
BEGIN
    CREATE INDEX [IX_AdministrativeInvestigations_IssueType_CreatedOnUtc] ON [AdministrativeInvestigations] ([IssueType], [CreatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013140941_Phase18AdministrativeInvestigations'
)
BEGIN
    CREATE INDEX [IX_AdministrativeInvestigations_LinkedBackgroundJobRunId] ON [AdministrativeInvestigations] ([LinkedBackgroundJobRunId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013140941_Phase18AdministrativeInvestigations'
)
BEGIN
    CREATE INDEX [IX_AdministrativeInvestigations_LinkedDoctorId] ON [AdministrativeInvestigations] ([LinkedDoctorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013140941_Phase18AdministrativeInvestigations'
)
BEGIN
    CREATE INDEX [IX_AdministrativeInvestigations_LinkedOutboxMessageId] ON [AdministrativeInvestigations] ([LinkedOutboxMessageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013140941_Phase18AdministrativeInvestigations'
)
BEGIN
    CREATE INDEX [IX_AdministrativeInvestigations_LinkedSpecializationRequestId] ON [AdministrativeInvestigations] ([LinkedSpecializationRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013140941_Phase18AdministrativeInvestigations'
)
BEGIN
    CREATE INDEX [IX_AdministrativeInvestigations_Status_Priority_CreatedOnUtc] ON [AdministrativeInvestigations] ([Status], [Priority], [CreatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013140941_Phase18AdministrativeInvestigations'
)
BEGIN
    CREATE INDEX [IX_InvestigationHistories_ActorApplicationUserId] ON [InvestigationHistories] ([ActorApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013140941_Phase18AdministrativeInvestigations'
)
BEGIN
    CREATE INDEX [IX_InvestigationHistories_AssignedSuperAdminId] ON [InvestigationHistories] ([AssignedSuperAdminId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013140941_Phase18AdministrativeInvestigations'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InvestigationHistories_CaseId_SequenceNumber] ON [InvestigationHistories] ([CaseId], [SequenceNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013140941_Phase18AdministrativeInvestigations'
)
BEGIN
    CREATE TRIGGER [TR_Immutable_InvestigationHistories] ON [InvestigationHistories] AFTER UPDATE, DELETE AS THROW 51018, 'Investigation history is immutable.', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013140941_Phase18AdministrativeInvestigations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013140941_Phase18AdministrativeInvestigations', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014084528_Phase18ExportAudit'
)
BEGIN
    CREATE TABLE [ReportExportAudits] (
        [Id] uniqueidentifier NOT NULL,
        [ActorApplicationUserId] uniqueidentifier NOT NULL,
        [ActorRole] nvarchar(30) NOT NULL,
        [ReportCode] nvarchar(50) NOT NULL,
        [Format] nvarchar(5) NOT NULL,
        [Language] nvarchar(2) NOT NULL,
        [AppliedFiltersJson] nvarchar(4000) NOT NULL,
        [Outcome] nvarchar(30) NOT NULL,
        [FailureCode] nvarchar(100) NULL,
        [ByteCount] int NULL,
        [RecordedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ReportExportAudits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReportExportAudits_ApplicationUsers_ActorApplicationUserId] FOREIGN KEY ([ActorApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014084528_Phase18ExportAudit'
)
BEGIN
    CREATE INDEX [IX_ReportExportAudits_ActorApplicationUserId_RecordedAtUtc] ON [ReportExportAudits] ([ActorApplicationUserId], [RecordedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014084528_Phase18ExportAudit'
)
BEGIN
    CREATE INDEX [IX_ReportExportAudits_ReportCode_RecordedAtUtc] ON [ReportExportAudits] ([ReportCode], [RecordedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014084528_Phase18ExportAudit'
)
BEGIN
    CREATE TRIGGER [TR_Immutable_ReportExportAudits] ON [ReportExportAudits] AFTER UPDATE, DELETE AS THROW 51018, 'Report export audit is immutable.', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014084528_Phase18ExportAudit'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014084528_Phase18ExportAudit', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085648_Phase18ReportingReconciliation'
)
BEGIN
    CREATE TABLE [ReportingRefreshRuns] (
        [Id] uniqueidentifier NOT NULL,
        [StartedAtUtc] datetime2 NOT NULL,
        [FinishedAtUtc] datetime2 NULL,
        [Status] nvarchar(20) NOT NULL,
        [LastPracticeId] uniqueidentifier NULL,
        [PracticesObserved] int NOT NULL,
        [SnapshotsWritten] int NOT NULL,
        [FailureCode] nvarchar(100) NULL,
        CONSTRAINT [PK_ReportingRefreshRuns] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085648_Phase18ReportingReconciliation'
)
BEGIN
    CREATE INDEX [IX_ReportingRefreshRuns_StartedAtUtc] ON [ReportingRefreshRuns] ([StartedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085648_Phase18ReportingReconciliation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014085648_Phase18ReportingReconciliation', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085853_Phase18DatabaseInvariants'
)
BEGIN
    CREATE TRIGGER [TR_OperatingSettlements_OneEffective] ON [OperatingSettlements] AFTER INSERT AS
    BEGIN
     SET NOCOUNT ON;
     IF EXISTS (SELECT 1 FROM inserted i JOIN [OperatingSettlements] s WITH (UPDLOCK,HOLDLOCK)
        ON s.[ObligationId] = i.[ObligationId] AND s.[Id] <> i.[Id]
        WHERE NOT EXISTS (SELECT 1 FROM [OperatingSettlementReversals] r WHERE r.[OriginalSettlementId] = s.[Id]))
        THROW 51018, 'An obligation can have only one unreversed settlement.', 1;
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085853_Phase18DatabaseInvariants'
)
BEGIN
    CREATE TRIGGER [TR_Immutable_DoctorInsightHistories] ON [DoctorInsightHistories] AFTER UPDATE, DELETE AS THROW 51018, 'Reporting evidence and history are immutable.', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085853_Phase18DatabaseInvariants'
)
BEGIN
    CREATE TRIGGER [TR_Immutable_PracticeReportingPolicySnapshots] ON [PracticeReportingPolicySnapshots] AFTER UPDATE, DELETE AS THROW 51018, 'Reporting evidence and history are immutable.', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085853_Phase18DatabaseInvariants'
)
BEGIN
    CREATE TRIGGER [TR_SalaryPlans_NoOverlap] ON [OperatingFinancialPlans] AFTER INSERT, UPDATE AS
    BEGIN
     SET NOCOUNT ON;
     IF EXISTS (SELECT 1 FROM inserted i JOIN [OperatingFinancialPlans] p WITH (UPDLOCK,HOLDLOCK)
     ON p.[DoctorId] = i.[DoctorId] AND p.[ReceptionId] = i.[ReceptionId] AND p.[Id] <> i.[Id] AND p.[Kind] = 1 AND i.[Kind] = 1
     AND (i.[EffectiveTo] IS NULL OR p.[EffectiveFrom] <= i.[EffectiveTo]) AND (p.[EffectiveTo] IS NULL OR p.[EffectiveTo] >= i.[EffectiveFrom])
     WHERE NOT (p.[Status] = 3 AND p.[EffectiveTo] = p.[EffectiveFrom] AND NOT EXISTS (SELECT 1 FROM [OperatingObligations] o WHERE o.[PlanId] = p.[Id]))
     AND NOT (i.[Status] = 3 AND i.[EffectiveTo] = i.[EffectiveFrom] AND NOT EXISTS (SELECT 1 FROM [OperatingObligations] o WHERE o.[PlanId] = i.[Id])))
     THROW 51018, 'Salary plan effective intervals cannot overlap.', 1;
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085853_Phase18DatabaseInvariants'
)
BEGIN
    CREATE TRIGGER [TR_NoDelete_OperatingFinancialPlans] ON [OperatingFinancialPlans] INSTEAD OF DELETE AS THROW 51018, 'Phase 18 records cannot be deleted.', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085853_Phase18DatabaseInvariants'
)
BEGIN
    CREATE TRIGGER [TR_NoDelete_OperatingObligations] ON [OperatingObligations] INSTEAD OF DELETE AS THROW 51018, 'Phase 18 records cannot be deleted.', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085853_Phase18DatabaseInvariants'
)
BEGIN
    CREATE TRIGGER [TR_NoDelete_DoctorInsightFollowUps] ON [DoctorInsightFollowUps] INSTEAD OF DELETE AS THROW 51018, 'Phase 18 records cannot be deleted.', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085853_Phase18DatabaseInvariants'
)
BEGIN
    CREATE TRIGGER [TR_NoDelete_ReceptionHandovers] ON [ReceptionHandovers] INSTEAD OF DELETE AS THROW 51018, 'Phase 18 records cannot be deleted.', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085853_Phase18DatabaseInvariants'
)
BEGIN
    CREATE TRIGGER [TR_NoDelete_AdministrativeInvestigations] ON [AdministrativeInvestigations] INSTEAD OF DELETE AS THROW 51018, 'Phase 18 records cannot be deleted.', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085853_Phase18DatabaseInvariants'
)
BEGIN
    CREATE TRIGGER [TR_InsightEvidence_Ownership] ON [DoctorInsightFollowUps] AFTER UPDATE AS
    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON i.[Id] = d.[Id] WHERE i.[DoctorId] <> d.[DoctorId] OR i.[PracticeId] <> d.[PracticeId] OR i.[MetricCode] <> d.[MetricCode] OR i.[EvidenceJson] <> d.[EvidenceJson])
    THROW 51018, 'Insight evidence and ownership are immutable.', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085853_Phase18DatabaseInvariants'
)
BEGIN
    CREATE TRIGGER [TR_Handover_Ownership] ON [ReceptionHandovers] AFTER UPDATE AS
    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON i.[Id] = d.[Id] WHERE i.[DoctorId] <> d.[DoctorId] OR i.[DoctorPracticeId] <> d.[DoctorPracticeId] OR i.[Summary] <> d.[Summary] OR i.[CreatedByApplicationUserId] <> d.[CreatedByApplicationUserId] OR ISNULL(i.[ReservationId],CONVERT(uniqueidentifier,0x0)) <> ISNULL(d.[ReservationId],CONVERT(uniqueidentifier,0x0)) OR ISNULL(i.[TicketId],CONVERT(uniqueidentifier,0x0)) <> ISNULL(d.[TicketId],CONVERT(uniqueidentifier,0x0)) OR ISNULL(i.[PreviousHandoverId],CONVERT(uniqueidentifier,0x0)) <> ISNULL(d.[PreviousHandoverId],CONVERT(uniqueidentifier,0x0)))
    THROW 51018, 'Handover ownership and original content are immutable.', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014085853_Phase18DatabaseInvariants'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014085853_Phase18DatabaseInvariants', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014095416_Phase18ExportScopeSnapshot'
)
BEGIN
    ALTER TABLE [ReportExportAudits] ADD [ScopeSnapshotJson] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014095416_Phase18ExportScopeSnapshot'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014095416_Phase18ExportScopeSnapshot', N'10.0.10');
END;

COMMIT;
GO

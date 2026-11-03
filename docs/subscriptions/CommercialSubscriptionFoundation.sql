BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    ALTER TABLE [Receptions] ADD [AccountStateRevision] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE TABLE [CommercialFeatures] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(200) NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [NameEn] nvarchar(200) NULL,
        [DescriptionAr] nvarchar(2000) NULL,
        [DescriptionEn] nvarchar(2000) NULL,
        [GroupCode] nvarchar(200) NOT NULL,
        [ImplementationStatus] int NOT NULL,
        [ReleaseReadiness] int NOT NULL,
        [IsActive] bit NOT NULL,
        [IsPubliclyVisible] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedOnUtc] datetime2(7) NOT NULL,
        [ModifiedOnUtc] datetime2(7) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CommercialFeatures] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE TABLE [CommercialProducts] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(200) NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [NameEn] nvarchar(200) NULL,
        [DescriptionAr] nvarchar(2000) NULL,
        [DescriptionEn] nvarchar(2000) NULL,
        [IsActive] bit NOT NULL,
        [IsPubliclySelectable] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedOnUtc] datetime2(7) NOT NULL,
        [ModifiedOnUtc] datetime2(7) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CommercialProducts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE TABLE [LegacyTransitionEnrollmentBatches] (
        [Id] uniqueidentifier NOT NULL,
        [StartedAtUtc] datetime2(7) NOT NULL,
        [EndsAtUtc] datetime2(7) NOT NULL,
        [DurationDays] int NOT NULL,
        [InitiatedByApplicationUserId] uniqueidentifier NOT NULL,
        [Status] nvarchar(200) NOT NULL,
        [CompletedAtUtc] datetime2(7) NULL,
        [CreatedOnUtc] datetime2(7) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_LegacyTransitionEnrollmentBatches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LegacyTransitionEnrollmentBatches_ApplicationUsers_InitiatedByApplicationUserId] FOREIGN KEY ([InitiatedByApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE TABLE [FeaturePermissionMappings] (
        [Id] uniqueidentifier NOT NULL,
        [FeatureId] uniqueidentifier NOT NULL,
        [PermissionId] uniqueidentifier NOT NULL,
        [RequiredAccessLevel] int NOT NULL,
        [CreatedOnUtc] datetime2(7) NOT NULL,
        CONSTRAINT [PK_FeaturePermissionMappings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FeaturePermissionMappings_CommercialFeatures_FeatureId] FOREIGN KEY ([FeatureId]) REFERENCES [CommercialFeatures] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FeaturePermissionMappings_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE TABLE [CommercialPlans] (
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [Code] nvarchar(200) NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [NameEn] nvarchar(200) NULL,
        [DescriptionAr] nvarchar(2000) NULL,
        [DescriptionEn] nvarchar(2000) NULL,
        [IsActive] bit NOT NULL,
        [IsPubliclySelectable] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedOnUtc] datetime2(7) NOT NULL,
        [ModifiedOnUtc] datetime2(7) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CommercialPlans] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CommercialPlans_CommercialProducts_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [CommercialProducts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE TABLE [DoctorSubscriptions] (
        [Id] uniqueidentifier NOT NULL,
        [DoctorId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [PlanId] uniqueidentifier NOT NULL,
        [SubscriptionKind] int NOT NULL,
        [Status] int NOT NULL,
        [BillingCycle] int NULL,
        [SelectedPracticeCount] int NULL,
        [PriceAmount] decimal(18,2) NOT NULL,
        [Currency] nvarchar(200) NOT NULL,
        [PracticeLimit] int NOT NULL,
        [ReceptionLimit] int NOT NULL,
        [StartsAtUtc] datetime2(7) NULL,
        [EndsAtUtc] datetime2(7) NULL,
        [TrialStartedAtUtc] datetime2(7) NULL,
        [TrialEndsAtUtc] datetime2(7) NULL,
        [TrialExtensionCount] int NOT NULL,
        [CatalogVersion] nvarchar(200) NOT NULL,
        [TermsSnapshotJson] nvarchar(max) NOT NULL,
        [WillNotRenew] bit NOT NULL,
        [CancellationRequestedOnUtc] datetime2(7) NULL,
        [ActivatedByApplicationUserId] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2(7) NOT NULL,
        [ModifiedOnUtc] datetime2(7) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_DoctorSubscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_DoctorSubscriptions_Terms] CHECK ([PracticeLimit] > 0 AND [ReceptionLimit] >= 0 AND [PriceAmount] >= 0),
        CONSTRAINT [FK_DoctorSubscriptions_ApplicationUsers_ActivatedByApplicationUserId] FOREIGN KEY ([ActivatedByApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DoctorSubscriptions_CommercialPlans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [CommercialPlans] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DoctorSubscriptions_CommercialProducts_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [CommercialProducts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DoctorSubscriptions_Doctors_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [Doctors] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE TABLE [PlanFeatureEntitlements] (
        [Id] uniqueidentifier NOT NULL,
        [PlanId] uniqueidentifier NOT NULL,
        [FeatureId] uniqueidentifier NOT NULL,
        [AccessLevel] int NOT NULL,
        [CreatedOnUtc] datetime2(7) NOT NULL,
        CONSTRAINT [PK_PlanFeatureEntitlements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlanFeatureEntitlements_CommercialFeatures_FeatureId] FOREIGN KEY ([FeatureId]) REFERENCES [CommercialFeatures] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PlanFeatureEntitlements_CommercialPlans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [CommercialPlans] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE TABLE [SubscriptionChangeRequests] (
        [Id] uniqueidentifier NOT NULL,
        [SubscriptionId] uniqueidentifier NOT NULL,
        [RequestedByApplicationUserId] uniqueidentifier NOT NULL,
        [ChangeType] int NOT NULL,
        [TargetPlanCode] nvarchar(200) NULL,
        [TargetPracticeCount] int NULL,
        [TargetBillingCycle] int NULL,
        [Status] int NOT NULL,
        [RequestedOnUtc] datetime2(7) NOT NULL,
        [ReviewedByApplicationUserId] uniqueidentifier NULL,
        [ReviewedOnUtc] datetime2(7) NULL,
        [RejectionReason] nvarchar(2000) NULL,
        [TargetPriceSnapshot] decimal(18,2) NOT NULL,
        [DifferenceAmount] decimal(18,2) NOT NULL,
        [EffectiveAtUtc] datetime2(7) NULL,
        [TargetTermsSnapshotJson] nvarchar(max) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SubscriptionChangeRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SubscriptionChangeRequests_ApplicationUsers_RequestedByApplicationUserId] FOREIGN KEY ([RequestedByApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SubscriptionChangeRequests_ApplicationUsers_ReviewedByApplicationUserId] FOREIGN KEY ([ReviewedByApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SubscriptionChangeRequests_DoctorSubscriptions_SubscriptionId] FOREIGN KEY ([SubscriptionId]) REFERENCES [DoctorSubscriptions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE TABLE [SubscriptionFeatureSnapshots] (
        [Id] uniqueidentifier NOT NULL,
        [SubscriptionId] uniqueidentifier NOT NULL,
        [FeatureId] uniqueidentifier NOT NULL,
        [FeatureCodeSnapshot] nvarchar(200) NOT NULL,
        [AccessLevel] int NOT NULL,
        [CreatedOnUtc] datetime2(7) NOT NULL,
        CONSTRAINT [PK_SubscriptionFeatureSnapshots] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SubscriptionFeatureSnapshots_CommercialFeatures_FeatureId] FOREIGN KEY ([FeatureId]) REFERENCES [CommercialFeatures] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SubscriptionFeatureSnapshots_DoctorSubscriptions_SubscriptionId] FOREIGN KEY ([SubscriptionId]) REFERENCES [DoctorSubscriptions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE TABLE [SubscriptionHistories] (
        [Id] uniqueidentifier NOT NULL,
        [SubscriptionId] uniqueidentifier NOT NULL,
        [EventType] nvarchar(200) NOT NULL,
        [PreviousStatus] int NULL,
        [NewStatus] int NULL,
        [ActorApplicationUserId] uniqueidentifier NULL,
        [OccurredOnUtc] datetime2(7) NOT NULL,
        [Reason] nvarchar(2000) NULL,
        [PreviousTermsSnapshotJson] nvarchar(max) NULL,
        [NewTermsSnapshotJson] nvarchar(max) NULL,
        [MetadataJson] nvarchar(max) NULL,
        [EventIdempotencyKey] nvarchar(450) NOT NULL,
        [CreatedOnUtc] datetime2(7) NOT NULL,
        CONSTRAINT [PK_SubscriptionHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SubscriptionHistories_ApplicationUsers_ActorApplicationUserId] FOREIGN KEY ([ActorApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SubscriptionHistories_DoctorSubscriptions_SubscriptionId] FOREIGN KEY ([SubscriptionId]) REFERENCES [DoctorSubscriptions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE TABLE [SubscriptionPayments] (
        [Id] uniqueidentifier NOT NULL,
        [SubscriptionId] uniqueidentifier NOT NULL,
        [PaymentType] int NOT NULL,
        [PaymentMethod] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] nvarchar(200) NOT NULL,
        [PaidAtUtc] datetime2(7) NOT NULL,
        [RecordedByApplicationUserId] uniqueidentifier NOT NULL,
        [Reference] nvarchar(200) NULL,
        [Notes] nvarchar(2000) NULL,
        [IdempotencyKey] nvarchar(450) NOT NULL,
        [CreatedOnUtc] datetime2(7) NOT NULL,
        CONSTRAINT [PK_SubscriptionPayments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SubscriptionPayments_ApplicationUsers_RecordedByApplicationUserId] FOREIGN KEY ([RecordedByApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SubscriptionPayments_DoctorSubscriptions_SubscriptionId] FOREIGN KEY ([SubscriptionId]) REFERENCES [DoctorSubscriptions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE TABLE [SubscriptionReminderSchedules] (
        [Id] uniqueidentifier NOT NULL,
        [SubscriptionId] uniqueidentifier NOT NULL,
        [ReminderCode] nvarchar(200) NOT NULL,
        [DueAtUtc] datetime2(7) NOT NULL,
        [TrialRevision] int NOT NULL,
        [Status] int NOT NULL,
        [CreatedOnUtc] datetime2(7) NOT NULL,
        [ProcessedOnUtc] datetime2(7) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SubscriptionReminderSchedules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SubscriptionReminderSchedules_DoctorSubscriptions_SubscriptionId] FOREIGN KEY ([SubscriptionId]) REFERENCES [DoctorSubscriptions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CommercialFeatures_Code] ON [CommercialFeatures] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CommercialPlans_ProductId_Code] ON [CommercialPlans] ([ProductId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CommercialProducts_Code] ON [CommercialProducts] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE INDEX [IX_DoctorSubscriptions_ActivatedByApplicationUserId] ON [DoctorSubscriptions] ([ActivatedByApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE INDEX [IX_DoctorSubscriptions_DoctorId_Status] ON [DoctorSubscriptions] ([DoctorId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE INDEX [IX_DoctorSubscriptions_PlanId] ON [DoctorSubscriptions] ([PlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE INDEX [IX_DoctorSubscriptions_ProductId] ON [DoctorSubscriptions] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE INDEX [IX_DoctorSubscriptions_Status_EndsAtUtc] ON [DoctorSubscriptions] ([Status], [EndsAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_OneActiveSubscriptionPerDoctor] ON [DoctorSubscriptions] ([DoctorId]) WHERE [Status] = 2');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_OnePendingSubscriptionPerDoctor] ON [DoctorSubscriptions] ([DoctorId]) WHERE [Status] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_OneScheduledSubscriptionPerDoctor] ON [DoctorSubscriptions] ([DoctorId]) WHERE [Status] = 6');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_OneTrialPerDoctor] ON [DoctorSubscriptions] ([DoctorId]) WHERE [SubscriptionKind] = 2');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE INDEX [IX_FeaturePermissionMappings_FeatureId] ON [FeaturePermissionMappings] ([FeatureId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FeaturePermissionMappings_PermissionId] ON [FeaturePermissionMappings] ([PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE INDEX [IX_LegacyTransitionEnrollmentBatches_InitiatedByApplicationUserId] ON [LegacyTransitionEnrollmentBatches] ([InitiatedByApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE INDEX [IX_PlanFeatureEntitlements_FeatureId] ON [PlanFeatureEntitlements] ([FeatureId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PlanFeatureEntitlements_PlanId_FeatureId] ON [PlanFeatureEntitlements] ([PlanId], [FeatureId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE INDEX [IX_SubscriptionChangeRequests_RequestedByApplicationUserId] ON [SubscriptionChangeRequests] ([RequestedByApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE INDEX [IX_SubscriptionChangeRequests_ReviewedByApplicationUserId] ON [SubscriptionChangeRequests] ([ReviewedByApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE INDEX [IX_SubscriptionChangeRequests_SubscriptionId_Status] ON [SubscriptionChangeRequests] ([SubscriptionId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_OnePendingChangePerSubscription] ON [SubscriptionChangeRequests] ([SubscriptionId]) WHERE [Status] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE INDEX [IX_SubscriptionFeatureSnapshots_FeatureId] ON [SubscriptionFeatureSnapshots] ([FeatureId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SubscriptionFeatureSnapshots_SubscriptionId_FeatureId] ON [SubscriptionFeatureSnapshots] ([SubscriptionId], [FeatureId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE INDEX [IX_SubscriptionHistories_ActorApplicationUserId] ON [SubscriptionHistories] ([ActorApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SubscriptionHistories_EventIdempotencyKey] ON [SubscriptionHistories] ([EventIdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE INDEX [IX_SubscriptionHistories_SubscriptionId_OccurredOnUtc] ON [SubscriptionHistories] ([SubscriptionId], [OccurredOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SubscriptionPayments_IdempotencyKey] ON [SubscriptionPayments] ([IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE INDEX [IX_SubscriptionPayments_RecordedByApplicationUserId] ON [SubscriptionPayments] ([RecordedByApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE INDEX [IX_SubscriptionPayments_SubscriptionId_PaidAtUtc] ON [SubscriptionPayments] ([SubscriptionId], [PaidAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE INDEX [IX_SubscriptionReminderSchedules_Status_DueAtUtc] ON [SubscriptionReminderSchedules] ([Status], [DueAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SubscriptionReminderSchedules_SubscriptionId_ReminderCode_TrialRevision] ON [SubscriptionReminderSchedules] ([SubscriptionId], [ReminderCode], [TrialRevision]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190031_CommercialSubscriptionFoundation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102190031_CommercialSubscriptionFoundation', N'10.0.10');
END;

COMMIT;
GO

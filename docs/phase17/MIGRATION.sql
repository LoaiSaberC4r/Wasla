BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    ALTER TABLE [EmailOutboxMessages] ADD [LastAttemptOnUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    ALTER TABLE [archive].[Reservations] ADD [AppointmentRevisionHistoryId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    ALTER TABLE [Reservations] ADD [AppointmentRevisionHistoryId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    ALTER TABLE [EmailOutboxMessages] ADD [NotificationDeliveryId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    CREATE TABLE [NotificationAudits] (
        [Id] uniqueidentifier NOT NULL,
        [ActorApplicationUserId] uniqueidentifier NOT NULL,
        [DeliveryId] uniqueidentifier NULL,
        [Action] nvarchar(80) NOT NULL,
        [OutcomeCode] nvarchar(80) NOT NULL,
        [Reason] nvarchar(500) NULL,
        [OccurredAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_NotificationAudits] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    CREATE TABLE [NotificationEvents] (
        [Id] uniqueidentifier NOT NULL,
        [EventType] nvarchar(80) NOT NULL,
        [SourceType] nvarchar(30) NOT NULL,
        [SourceId] uniqueidentifier NOT NULL,
        [SourceEventKey] nvarchar(100) NOT NULL,
        [SourceRevisionKey] nvarchar(100) NOT NULL,
        [SourceVersionId] uniqueidentifier NULL,
        [PatientId] uniqueidentifier NULL,
        [DoctorId] uniqueidentifier NULL,
        [DoctorPracticeId] uniqueidentifier NULL,
        [TargetApplicationUserId] uniqueidentifier NULL,
        [Category] int NOT NULL,
        [DataClassification] int NOT NULL,
        [OccurredAtUtc] datetime2 NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [LegacyEmailOwned] bit NOT NULL,
        [IdempotencyKey] nvarchar(300) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_NotificationEvents] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    CREATE TABLE [NotificationPreferences] (
        [Id] uniqueidentifier NOT NULL,
        [ApplicationUserId] uniqueidentifier NOT NULL,
        [Category] int NOT NULL,
        [Channel] int NOT NULL,
        [Enabled] bit NOT NULL,
        [PreferredLanguage] nvarchar(2) NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_NotificationPreferences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NotificationPreferences_ApplicationUsers_ApplicationUserId] FOREIGN KEY ([ApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    CREATE TABLE [PlatformNotificationSettings] (
        [Id] uniqueidentifier NOT NULL,
        [AppointmentReminderEnabled] bit NOT NULL,
        [ReminderLeadMinutes] int NOT NULL,
        [Version] bigint NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [UpdatedByApplicationUserId] uniqueidentifier NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PlatformNotificationSettings] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_NotificationSettings_Lead] CHECK ([ReminderLeadMinutes] BETWEEN 1 AND 43200),
        CONSTRAINT [CK_NotificationSettings_Singleton] CHECK ([Id] = '17000000-0000-0000-0000-000000000001')
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    CREATE TABLE [PlatformNotificationSettingsAudits] (
        [Id] uniqueidentifier NOT NULL,
        [ActorApplicationUserId] uniqueidentifier NOT NULL,
        [PreviousVersion] bigint NOT NULL,
        [NewVersion] bigint NOT NULL,
        [PreviousReminderLeadMinutes] int NOT NULL,
        [NewReminderLeadMinutes] int NOT NULL,
        [PreviousEnabled] bit NOT NULL,
        [NewEnabled] bit NOT NULL,
        [ChangedAtUtc] datetime2 NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_PlatformNotificationSettingsAudits] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    CREATE TABLE [NotificationIntents] (
        [Id] uniqueidentifier NOT NULL,
        [NotificationEventId] uniqueidentifier NOT NULL,
        [DueAtUtc] datetime2 NOT NULL,
        [ExpiresAtUtc] datetime2 NULL,
        [Status] int NOT NULL,
        [SourceRevisionKey] nvarchar(100) NOT NULL,
        [AppointmentStartUtcSnapshot] datetime2 NULL,
        [ReminderLeadMinutesSnapshot] int NULL,
        [SettingsVersionSnapshot] bigint NULL,
        [AttemptCount] int NOT NULL,
        [NextAttemptOnUtc] datetime2 NULL,
        [ProcessingToken] uniqueidentifier NULL,
        [LeaseUntilUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [ProcessedAtUtc] datetime2 NULL,
        [SkipReasonCode] nvarchar(80) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_NotificationIntents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NotificationIntents_NotificationEvents_NotificationEventId] FOREIGN KEY ([NotificationEventId]) REFERENCES [NotificationEvents] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    CREATE TABLE [NotificationDeliveries] (
        [Id] uniqueidentifier NOT NULL,
        [NotificationEventId] uniqueidentifier NOT NULL,
        [NotificationIntentId] uniqueidentifier NOT NULL,
        [Channel] int NOT NULL,
        [Status] int NOT NULL,
        [RecipientLogicalKey] nvarchar(100) NOT NULL,
        [RecipientApplicationUserId] uniqueidentifier NULL,
        [RecipientPatientId] uniqueidentifier NULL,
        [TemplateCode] nvarchar(80) NOT NULL,
        [TemplateVersion] int NOT NULL,
        [LogicalDeliveryKey] nvarchar(450) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [DispatchedAtUtc] datetime2 NULL,
        [SafeFailureCode] nvarchar(80) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_NotificationDeliveries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NotificationDeliveries_NotificationEvents_NotificationEventId] FOREIGN KEY ([NotificationEventId]) REFERENCES [NotificationEvents] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_NotificationDeliveries_NotificationIntents_NotificationIntentId] FOREIGN KEY ([NotificationIntentId]) REFERENCES [NotificationIntents] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    CREATE TABLE [UserNotifications] (
        [Id] uniqueidentifier NOT NULL,
        [NotificationDeliveryId] uniqueidentifier NOT NULL,
        [ApplicationUserId] uniqueidentifier NOT NULL,
        [Category] int NOT NULL,
        [EventType] nvarchar(80) NOT NULL,
        [TemplateCode] nvarchar(80) NOT NULL,
        [TemplateVersion] int NOT NULL,
        [SafePreviewCode] nvarchar(80) NOT NULL,
        [TargetResourceType] nvarchar(30) NOT NULL,
        [TargetResourceKey] nvarchar(50) NULL,
        [OccurredAtUtc] datetime2 NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [ReadAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_UserNotifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserNotifications_ApplicationUsers_ApplicationUserId] FOREIGN KEY ([ApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserNotifications_NotificationDeliveries_NotificationDeliveryId] FOREIGN KEY ([NotificationDeliveryId]) REFERENCES [NotificationDeliveries] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AppointmentReminderEnabled', N'ReminderLeadMinutes', N'UpdatedAtUtc', N'UpdatedByApplicationUserId', N'Version') AND [object_id] = OBJECT_ID(N'[PlatformNotificationSettings]'))
        SET IDENTITY_INSERT [PlatformNotificationSettings] ON;
    EXEC(N'INSERT INTO [PlatformNotificationSettings] ([Id], [AppointmentReminderEnabled], [ReminderLeadMinutes], [UpdatedAtUtc], [UpdatedByApplicationUserId], [Version])
    VALUES (''17000000-0000-0000-0000-000000000001'', CAST(1 AS bit), 120, ''2026-10-25T00:00:00.0000000Z'', NULL, CAST(1 AS bigint))');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AppointmentReminderEnabled', N'ReminderLeadMinutes', N'UpdatedAtUtc', N'UpdatedByApplicationUserId', N'Version') AND [object_id] = OBJECT_ID(N'[PlatformNotificationSettings]'))
        SET IDENTITY_INSERT [PlatformNotificationSettings] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Reservations_AppointmentRevisionHistoryId] ON [Reservations] ([AppointmentRevisionHistoryId]) WHERE [AppointmentRevisionHistoryId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_EmailOutboxMessages_NotificationDeliveryId] ON [EmailOutboxMessages] ([NotificationDeliveryId]) WHERE [NotificationDeliveryId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    CREATE INDEX [IX_NotificationAudits_OccurredAtUtc] ON [NotificationAudits] ([OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NotificationDeliveries_LogicalDeliveryKey] ON [NotificationDeliveries] ([LogicalDeliveryKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    CREATE INDEX [IX_NotificationDeliveries_NotificationEventId] ON [NotificationDeliveries] ([NotificationEventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    CREATE INDEX [IX_NotificationDeliveries_NotificationIntentId] ON [NotificationDeliveries] ([NotificationIntentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    CREATE INDEX [IX_NotificationDeliveries_Status_CreatedAtUtc] ON [NotificationDeliveries] ([Status], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NotificationEvents_IdempotencyKey] ON [NotificationEvents] ([IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    CREATE INDEX [IX_NotificationEvents_SourceType_SourceId_SourceRevisionKey] ON [NotificationEvents] ([SourceType], [SourceId], [SourceRevisionKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NotificationIntents_NotificationEventId] ON [NotificationIntents] ([NotificationEventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    CREATE INDEX [IX_NotificationIntents_Status_DueAtUtc] ON [NotificationIntents] ([Status], [DueAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    CREATE INDEX [IX_NotificationIntents_Status_NextAttemptOnUtc] ON [NotificationIntents] ([Status], [NextAttemptOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NotificationPreferences_ApplicationUserId_Category_Channel] ON [NotificationPreferences] ([ApplicationUserId], [Category], [Channel]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PlatformNotificationSettingsAudits_NewVersion] ON [PlatformNotificationSettingsAudits] ([NewVersion]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    CREATE INDEX [IX_UserNotifications_ApplicationUserId_CreatedAtUtc_Id] ON [UserNotifications] ([ApplicationUserId], [CreatedAtUtc] DESC, [Id] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    CREATE INDEX [IX_UserNotifications_ApplicationUserId_ReadAtUtc_CreatedAtUtc] ON [UserNotifications] ([ApplicationUserId], [ReadAtUtc], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserNotifications_NotificationDeliveryId_ApplicationUserId] ON [UserNotifications] ([NotificationDeliveryId], [ApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    ALTER TABLE [EmailOutboxMessages] ADD CONSTRAINT [FK_EmailOutboxMessages_NotificationDeliveries_NotificationDeliveryId] FOREIGN KEY ([NotificationDeliveryId]) REFERENCES [NotificationDeliveries] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025114416_Phase17NotificationsAndReminders'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261025114416_Phase17NotificationsAndReminders', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025120012_Phase17VerifiedNotificationContacts'
)
BEGIN
    CREATE TABLE [NotificationVerifiedContacts] (
        [Id] uniqueidentifier NOT NULL,
        [ApplicationUserId] uniqueidentifier NOT NULL,
        [PatientId] uniqueidentifier NOT NULL,
        [AddressFingerprint] nvarchar(64) NOT NULL,
        [EvidenceId] uniqueidentifier NOT NULL,
        [VerifiedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_NotificationVerifiedContacts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NotificationVerifiedContacts_ApplicationUsers_ApplicationUserId] FOREIGN KEY ([ApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_NotificationVerifiedContacts_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025120012_Phase17VerifiedNotificationContacts'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NotificationVerifiedContacts_ApplicationUserId_PatientId_AddressFingerprint] ON [NotificationVerifiedContacts] ([ApplicationUserId], [PatientId], [AddressFingerprint]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025120012_Phase17VerifiedNotificationContacts'
)
BEGIN
    CREATE INDEX [IX_NotificationVerifiedContacts_PatientId] ON [NotificationVerifiedContacts] ([PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025120012_Phase17VerifiedNotificationContacts'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261025120012_Phase17VerifiedNotificationContacts', N'10.0.10');
END;

COMMIT;
GO

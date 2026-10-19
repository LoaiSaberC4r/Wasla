BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    IF EXISTS (SELECT 1 FROM [Patients] WHERE [Email] IS NOT NULL AND LEN(LTRIM(RTRIM([Email]))) = 0)
        OR EXISTS (SELECT LOWER(LTRIM(RTRIM([Email]))) FROM [Patients] WHERE [Email] IS NOT NULL GROUP BY LOWER(LTRIM(RTRIM([Email]))) HAVING COUNT_BIG(*) > 1)
    THROW 51050, 'Phase 5 identity migration stopped: incompatible patient email rows. No cleanup or merge is performed. Review the preflight and use a separately operator-authorized disposable database if appropriate.', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    ALTER TABLE [Patients] ADD [ReceptionRegisteredOnUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    ALTER TABLE [Patients] ADD [RegistrationDoctorPracticeId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    ALTER TABLE [Patients] ADD [RegistrationReceptionApplicationUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    ALTER TABLE [PatientContacts] ADD [ContactEmail] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    EXEC(N'ALTER TABLE [Patients] ADD [NormalizedEmail] AS LOWER(LTRIM(RTRIM([Email]))) PERSISTED');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    EXEC(N'ALTER TABLE [Patients] ADD [NormalizedSearchNameAr] AS UPPER(LTRIM(RTRIM([NameAr]))) PERSISTED');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    EXEC(N'ALTER TABLE [Patients] ADD [NormalizedSearchNameEn] AS UPPER(LTRIM(RTRIM([NameEn]))) PERSISTED');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE TABLE [PatientDuplicateCases] (
        [Id] uniqueidentifier NOT NULL,
        [FirstPatientId] uniqueidentifier NOT NULL,
        [SecondPatientId] uniqueidentifier NOT NULL,
        [LowerPatientId] uniqueidentifier NOT NULL,
        [HigherPatientId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL,
        [Trigger] int NOT NULL,
        [MatchIndicators] nvarchar(1000) NOT NULL,
        [CreatedByApplicationUserId] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [ResolvedBySuperAdminApplicationUserId] uniqueidentifier NULL,
        [ResolvedOnUtc] datetime2 NULL,
        [ResolutionReason] nvarchar(1000) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PatientDuplicateCases] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PatientDuplicateCases_Distinct] CHECK ([FirstPatientId] <> [SecondPatientId]),
        CONSTRAINT [FK_PatientDuplicateCases_ApplicationUsers_CreatedByApplicationUserId] FOREIGN KEY ([CreatedByApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientDuplicateCases_ApplicationUsers_ResolvedBySuperAdminApplicationUserId] FOREIGN KEY ([ResolvedBySuperAdminApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientDuplicateCases_Patients_FirstPatientId] FOREIGN KEY ([FirstPatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientDuplicateCases_Patients_SecondPatientId] FOREIGN KEY ([SecondPatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE TABLE [PatientIdentityAttestations] (
        [Id] uniqueidentifier NOT NULL,
        [PatientId] uniqueidentifier NOT NULL,
        [DoctorPracticeId] uniqueidentifier NOT NULL,
        [ReceptionApplicationUserId] uniqueidentifier NOT NULL,
        [VerificationBasis] int NOT NULL,
        [Purpose] int NOT NULL,
        [VerifiedClaimantNormalizedEmail] nvarchar(200) NULL,
        [RelatedReservationId] uniqueidentifier NULL,
        [RelatedTicketId] uniqueidentifier NULL,
        [OccurredOnUtc] datetime2 NOT NULL,
        [ExpiresOnUtc] datetime2 NULL,
        [Reason] nvarchar(500) NULL,
        [ConsumedOnUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PatientIdentityAttestations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PatientIdentityAttestations_ApplicationUsers_ReceptionApplicationUserId] FOREIGN KEY ([ReceptionApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientIdentityAttestations_DoctorPractices_DoctorPracticeId] FOREIGN KEY ([DoctorPracticeId]) REFERENCES [DoctorPractices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientIdentityAttestations_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE TABLE [PatientOnboardingChallenges] (
        [Id] uniqueidentifier NOT NULL,
        [Purpose] int NOT NULL,
        [NormalizedEmail] nvarchar(200) NOT NULL,
        [ApplicationUserId] uniqueidentifier NULL,
        [PatientReference] uniqueidentifier NULL,
        [OtpHash] nvarchar(256) NOT NULL,
        [AttemptCount] int NOT NULL,
        [MaxAttempts] int NOT NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [OtpExpiresOnUtc] datetime2 NOT NULL,
        [LastSentOnUtc] datetime2 NOT NULL,
        [VerifiedOnUtc] datetime2 NULL,
        [ContinuationTokenHash] nvarchar(256) NULL,
        [ContinuationTokenExpiresOnUtc] datetime2 NULL,
        [AllowedAction] int NULL,
        [ConsumedOnUtc] datetime2 NULL,
        [InvalidatedOnUtc] datetime2 NULL,
        [IdempotencyFingerprint] nvarchar(256) NULL,
        [MutationIdempotencyKey] nvarchar(100) NULL,
        [MutationResponseJson] nvarchar(4000) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PatientOnboardingChallenges] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PatientOnboardingChallenges_ApplicationUsers_ApplicationUserId] FOREIGN KEY ([ApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE TABLE [PatientClaimRequests] (
        [Id] uniqueidentifier NOT NULL,
        [TargetPatientId] uniqueidentifier NULL,
        [ClaimantNormalizedEmail] nvarchar(200) NOT NULL,
        [PatientReference] uniqueidentifier NULL,
        [Status] int NOT NULL,
        [ApplicantNameAr] nvarchar(200) NOT NULL,
        [ApplicantDateOfBirth] date NOT NULL,
        [ApplicantGender] int NOT NULL,
        [ApplicantPhoneNumber] nvarchar(30) NULL,
        [SourceChallengeId] uniqueidentifier NOT NULL,
        [EvidenceRevision] int NOT NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [ModifiedOnUtc] datetime2 NULL,
        [SubmittedOnUtc] datetime2 NOT NULL,
        [LastActivityOnUtc] datetime2 NOT NULL,
        [ReviewedOnUtc] datetime2 NULL,
        [ReviewedByApplicationUserId] uniqueidentifier NULL,
        [DecisionReason] nvarchar(1000) NULL,
        [ApprovedTargetPatientId] uniqueidentifier NULL,
        [LinkedApplicationUserId] uniqueidentifier NULL,
        [ClosedOnUtc] datetime2 NULL,
        [IsUnderLegalHold] bit NOT NULL,
        [LegalHoldReason] nvarchar(1000) NULL,
        [LegalHoldSetByApplicationUserId] uniqueidentifier NULL,
        [LegalHoldSetOnUtc] datetime2 NULL,
        [IdempotencyKey] nvarchar(100) NOT NULL,
        [RequestFingerprint] nvarchar(256) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PatientClaimRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PatientClaimRequests_ApplicationUsers_LegalHoldSetByApplicationUserId] FOREIGN KEY ([LegalHoldSetByApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientClaimRequests_ApplicationUsers_LinkedApplicationUserId] FOREIGN KEY ([LinkedApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientClaimRequests_ApplicationUsers_ReviewedByApplicationUserId] FOREIGN KEY ([ReviewedByApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientClaimRequests_PatientOnboardingChallenges_SourceChallengeId] FOREIGN KEY ([SourceChallengeId]) REFERENCES [PatientOnboardingChallenges] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientClaimRequests_Patients_ApprovedTargetPatientId] FOREIGN KEY ([ApprovedTargetPatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientClaimRequests_Patients_TargetPatientId] FOREIGN KEY ([TargetPatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE TABLE [PatientActivationInvitations] (
        [Id] uniqueidentifier NOT NULL,
        [Purpose] int NOT NULL,
        [ApplicationUserId] uniqueidentifier NULL,
        [ClaimRequestId] uniqueidentifier NULL,
        [NormalizedEmail] nvarchar(200) NOT NULL,
        [TokenHash] nvarchar(256) NOT NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [ExpiresOnUtc] datetime2 NOT NULL,
        [ConsumedOnUtc] datetime2 NULL,
        [RevokedOnUtc] datetime2 NULL,
        [SendCount] int NOT NULL,
        [ResendIdempotencyKey] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PatientActivationInvitations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PatientActivationInvitations_ApplicationUsers_ApplicationUserId] FOREIGN KEY ([ApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientActivationInvitations_PatientClaimRequests_ClaimRequestId] FOREIGN KEY ([ClaimRequestId]) REFERENCES [PatientClaimRequests] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE TABLE [PatientClaimEvidences] (
        [Id] uniqueidentifier NOT NULL,
        [ClaimRequestId] uniqueidentifier NOT NULL,
        [RevisionNumber] int NOT NULL,
        [EvidenceType] int NOT NULL,
        [PrivateMediaKey] nvarchar(1000) NULL,
        [ContentType] nvarchar(200) NULL,
        [SizeBytes] bigint NULL,
        [Sha256Hash] nvarchar(256) NULL,
        [UploadedByType] int NOT NULL,
        [UploadedByApplicationUserId] uniqueidentifier NULL,
        [IdentityAttestationId] uniqueidentifier NULL,
        [UploadedOnUtc] datetime2 NOT NULL,
        [RetiredOnUtc] datetime2 NULL,
        [PurgedOnUtc] datetime2 NULL,
        CONSTRAINT [PK_PatientClaimEvidences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PatientClaimEvidences_ApplicationUsers_UploadedByApplicationUserId] FOREIGN KEY ([UploadedByApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientClaimEvidences_PatientClaimRequests_ClaimRequestId] FOREIGN KEY ([ClaimRequestId]) REFERENCES [PatientClaimRequests] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientClaimEvidences_PatientIdentityAttestations_IdentityAttestationId] FOREIGN KEY ([IdentityAttestationId]) REFERENCES [PatientIdentityAttestations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE TABLE [PatientClaimHistories] (
        [Id] uniqueidentifier NOT NULL,
        [ClaimRequestId] uniqueidentifier NOT NULL,
        [RevisionNumber] int NOT NULL,
        [FromStatus] int NULL,
        [ToStatus] int NOT NULL,
        [Action] int NOT NULL,
        [ActorApplicationUserId] uniqueidentifier NULL,
        [OccurredOnUtc] datetime2 NOT NULL,
        [Reason] nvarchar(1000) NULL,
        [SafeMetadataJson] nvarchar(4000) NULL,
        CONSTRAINT [PK_PatientClaimHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PatientClaimHistories_ApplicationUsers_ActorApplicationUserId] FOREIGN KEY ([ActorApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientClaimHistories_PatientClaimRequests_ClaimRequestId] FOREIGN KEY ([ClaimRequestId]) REFERENCES [PatientClaimRequests] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_Patients_NormalizedSearchNameAr_DateOfBirth_Gender] ON [Patients] ([NormalizedSearchNameAr], [DateOfBirth], [Gender]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_Patients_NormalizedSearchNameEn_DateOfBirth] ON [Patients] ([NormalizedSearchNameEn], [DateOfBirth]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Patients_NormalizedEmail] ON [Patients] ([NormalizedEmail]) WHERE [Email] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    EXEC(N'ALTER TABLE [Patients] ADD CONSTRAINT [CK_Patients_Email] CHECK ([Email] IS NULL OR LEN(LTRIM(RTRIM([Email]))) > 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PatientActivationInvitations_ApplicationUserId] ON [PatientActivationInvitations] ([ApplicationUserId]) WHERE [ApplicationUserId] IS NOT NULL AND [ConsumedOnUtc] IS NULL AND [RevokedOnUtc] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PatientActivationInvitations_ClaimRequestId] ON [PatientActivationInvitations] ([ClaimRequestId]) WHERE [ClaimRequestId] IS NOT NULL AND [ConsumedOnUtc] IS NULL AND [RevokedOnUtc] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientActivationInvitations_NormalizedEmail_CreatedOnUtc] ON [PatientActivationInvitations] ([NormalizedEmail], [CreatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PatientActivationInvitations_ResendIdempotencyKey] ON [PatientActivationInvitations] ([ResendIdempotencyKey]) WHERE [ResendIdempotencyKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PatientActivationInvitations_TokenHash] ON [PatientActivationInvitations] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientClaimEvidences_ClaimRequestId_RevisionNumber] ON [PatientClaimEvidences] ([ClaimRequestId], [RevisionNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientClaimEvidences_IdentityAttestationId] ON [PatientClaimEvidences] ([IdentityAttestationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientClaimEvidences_RetiredOnUtc_PurgedOnUtc] ON [PatientClaimEvidences] ([RetiredOnUtc], [PurgedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientClaimEvidences_UploadedByApplicationUserId] ON [PatientClaimEvidences] ([UploadedByApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientClaimHistories_ActorApplicationUserId] ON [PatientClaimHistories] ([ActorApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientClaimHistories_ClaimRequestId_OccurredOnUtc_Id] ON [PatientClaimHistories] ([ClaimRequestId], [OccurredOnUtc], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientClaimRequests_ApprovedTargetPatientId] ON [PatientClaimRequests] ([ApprovedTargetPatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PatientClaimRequests_ClaimantNormalizedEmail_IdempotencyKey] ON [PatientClaimRequests] ([ClaimantNormalizedEmail], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientClaimRequests_IsUnderLegalHold_ClosedOnUtc] ON [PatientClaimRequests] ([IsUnderLegalHold], [ClosedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientClaimRequests_LegalHoldSetByApplicationUserId] ON [PatientClaimRequests] ([LegalHoldSetByApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientClaimRequests_LinkedApplicationUserId] ON [PatientClaimRequests] ([LinkedApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientClaimRequests_ReviewedByApplicationUserId] ON [PatientClaimRequests] ([ReviewedByApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientClaimRequests_SourceChallengeId] ON [PatientClaimRequests] ([SourceChallengeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientClaimRequests_Status_LastActivityOnUtc] ON [PatientClaimRequests] ([Status], [LastActivityOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientClaimRequests_Status_SubmittedOnUtc] ON [PatientClaimRequests] ([Status], [SubmittedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_PatientClaims_OpenEmail] ON [PatientClaimRequests] ([ClaimantNormalizedEmail]) WHERE [TargetPatientId] IS NULL AND [Status] IN (1, 2, 3)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_PatientClaims_OpenTarget] ON [PatientClaimRequests] ([TargetPatientId]) WHERE [TargetPatientId] IS NOT NULL AND [Status] IN (1, 2, 3)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientDuplicateCases_CreatedByApplicationUserId] ON [PatientDuplicateCases] ([CreatedByApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientDuplicateCases_FirstPatientId] ON [PatientDuplicateCases] ([FirstPatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PatientDuplicateCases_LowerPatientId_HigherPatientId] ON [PatientDuplicateCases] ([LowerPatientId], [HigherPatientId]) WHERE [Status] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientDuplicateCases_ResolvedBySuperAdminApplicationUserId] ON [PatientDuplicateCases] ([ResolvedBySuperAdminApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientDuplicateCases_SecondPatientId] ON [PatientDuplicateCases] ([SecondPatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientDuplicateCases_Status] ON [PatientDuplicateCases] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientIdentityAttestations_DoctorPracticeId_PatientId_ReceptionApplicationUserId_ExpiresOnUtc] ON [PatientIdentityAttestations] ([DoctorPracticeId], [PatientId], [ReceptionApplicationUserId], [ExpiresOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientIdentityAttestations_PatientId] ON [PatientIdentityAttestations] ([PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientIdentityAttestations_ReceptionApplicationUserId] ON [PatientIdentityAttestations] ([ReceptionApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientOnboardingChallenges_ApplicationUserId] ON [PatientOnboardingChallenges] ([ApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PatientOnboardingChallenges_ContinuationTokenHash] ON [PatientOnboardingChallenges] ([ContinuationTokenHash]) WHERE [ContinuationTokenHash] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientOnboardingChallenges_NormalizedEmail_Purpose_CreatedOnUtc] ON [PatientOnboardingChallenges] ([NormalizedEmail], [Purpose], [CreatedOnUtc] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    CREATE INDEX [IX_PatientOnboardingChallenges_OtpExpiresOnUtc] ON [PatientOnboardingChallenges] ([OtpExpiresOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018140120_Phase5FinalPatientIdentityClosure'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261018140120_Phase5FinalPatientIdentityClosure', N'10.0.10');
END;

COMMIT;
GO


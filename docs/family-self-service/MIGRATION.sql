BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE TABLE [ChildOnboardingRequests] (
        [Id] uniqueidentifier NOT NULL,
        [ActorApplicationUserId] uniqueidentifier NOT NULL,
        [ParentPatientId] uniqueidentifier NOT NULL,
        [ParentRole] int NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [NameEn] nvarchar(200) NULL,
        [DateOfBirth] date NOT NULL,
        [Gender] int NOT NULL,
        [PhoneNumber] nvarchar(30) NULL,
        [ContactNameAr] nvarchar(200) NOT NULL,
        [ContactPhone] nvarchar(30) NOT NULL,
        [IdempotencyKey] nvarchar(100) NOT NULL,
        [Fingerprint] nvarchar(64) NOT NULL,
        [IntentHash] nvarchar(64) NOT NULL,
        [Status] int NOT NULL,
        [Revision] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [ReviewedByApplicationUserId] uniqueidentifier NULL,
        [ReviewedAtUtc] datetime2 NULL,
        [ReviewMessage] nvarchar(2000) NULL,
        [ApprovedPatientId] uniqueidentifier NULL,
        [FamilyRelationshipRequestId] uniqueidentifier NULL,
        [ReviewIdempotencyKey] nvarchar(100) NULL,
        [ReviewFingerprint] nvarchar(64) NULL,
        [ResubmitIdempotencyKey] nvarchar(100) NULL,
        [ResubmitFingerprint] nvarchar(64) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ChildOnboardingRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ChildOnboarding_Revision] CHECK ([Revision] >= 1),
        CONSTRAINT [CK_ChildOnboarding_Role] CHECK ([ParentRole] IN (1,2)),
        CONSTRAINT [CK_ChildOnboarding_Status] CHECK ([Status] IN (1,2,3,4)),
        CONSTRAINT [FK_ChildOnboardingRequests_ApplicationUsers_ActorApplicationUserId] FOREIGN KEY ([ActorApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ChildOnboardingRequests_ApplicationUsers_ReviewedByApplicationUserId] FOREIGN KEY ([ReviewedByApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ChildOnboardingRequests_FamilyRelationshipRequests_FamilyRelationshipRequestId] FOREIGN KEY ([FamilyRelationshipRequestId]) REFERENCES [FamilyRelationshipRequests] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ChildOnboardingRequests_Patients_ApprovedPatientId] FOREIGN KEY ([ApprovedPatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ChildOnboardingRequests_Patients_ParentPatientId] FOREIGN KEY ([ParentPatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE TABLE [FamilyCandidateSelections] (
        [Id] uniqueidentifier NOT NULL,
        [ActorApplicationUserId] uniqueidentifier NOT NULL,
        [ParentPatientId] uniqueidentifier NOT NULL,
        [TargetPatientId] uniqueidentifier NOT NULL,
        [TokenHash] nvarchar(64) NOT NULL,
        [CriteriaHash] nvarchar(64) NOT NULL,
        [Purpose] nvarchar(50) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [ConsumedAtUtc] datetime2 NULL,
        [IdempotencyKey] nvarchar(100) NULL,
        [SubmissionFingerprint] nvarchar(64) NULL,
        [RelationshipRequestId] uniqueidentifier NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_FamilyCandidateSelections] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FamilyCandidateSelections_ApplicationUsers_ActorApplicationUserId] FOREIGN KEY ([ActorApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FamilyCandidateSelections_FamilyRelationshipRequests_RelationshipRequestId] FOREIGN KEY ([RelationshipRequestId]) REFERENCES [FamilyRelationshipRequests] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FamilyCandidateSelections_Patients_ParentPatientId] FOREIGN KEY ([ParentPatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FamilyCandidateSelections_Patients_TargetPatientId] FOREIGN KEY ([TargetPatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE TABLE [ParentChildOperationalAuthorities] (
        [Id] uniqueidentifier NOT NULL,
        [FamilyId] uniqueidentifier NOT NULL,
        [ParentPatientId] uniqueidentifier NOT NULL,
        [ChildPatientId] uniqueidentifier NOT NULL,
        [ParentFamilyMemberId] uniqueidentifier NOT NULL,
        [ChildFamilyMemberId] uniqueidentifier NOT NULL,
        [ParentRole] int NOT NULL,
        [ApprovedRelationshipRequestId] uniqueidentifier NOT NULL,
        [ApprovedRevisionNumber] int NOT NULL,
        [ReviewedByApplicationUserId] uniqueidentifier NOT NULL,
        [ApprovedAtUtc] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        [RevokedAtUtc] datetime2 NULL,
        [RevocationReason] nvarchar(1000) NULL,
        [RevokedByApplicationUserId] uniqueidentifier NULL,
        [RevocationIdempotencyKey] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ParentChildOperationalAuthorities] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_OperationalAuthority_Distinct] CHECK ([ParentPatientId] <> [ChildPatientId] AND [ParentFamilyMemberId] <> [ChildFamilyMemberId]),
        CONSTRAINT [CK_OperationalAuthority_Revision] CHECK ([ApprovedRevisionNumber] >= 1),
        CONSTRAINT [CK_OperationalAuthority_Role] CHECK ([ParentRole] IN (1,2,5)),
        CONSTRAINT [FK_ParentChildOperationalAuthorities_ApplicationUsers_ReviewedByApplicationUserId] FOREIGN KEY ([ReviewedByApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ParentChildOperationalAuthorities_ApplicationUsers_RevokedByApplicationUserId] FOREIGN KEY ([RevokedByApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ParentChildOperationalAuthorities_Families_FamilyId] FOREIGN KEY ([FamilyId]) REFERENCES [Families] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ParentChildOperationalAuthorities_FamilyMembers_ChildFamilyMemberId] FOREIGN KEY ([ChildFamilyMemberId]) REFERENCES [FamilyMembers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ParentChildOperationalAuthorities_FamilyMembers_ParentFamilyMemberId] FOREIGN KEY ([ParentFamilyMemberId]) REFERENCES [FamilyMembers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ParentChildOperationalAuthorities_FamilyRelationshipRequests_ApprovedRelationshipRequestId] FOREIGN KEY ([ApprovedRelationshipRequestId]) REFERENCES [FamilyRelationshipRequests] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ParentChildOperationalAuthorities_Patients_ChildPatientId] FOREIGN KEY ([ChildPatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ParentChildOperationalAuthorities_Patients_ParentPatientId] FOREIGN KEY ([ParentPatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE TABLE [ChildOnboardingEvidence] (
        [Id] uniqueidentifier NOT NULL,
        [RequestId] uniqueidentifier NOT NULL,
        [Revision] int NOT NULL,
        [DocumentType] int NOT NULL,
        [MediaKey] nvarchar(1000) NOT NULL,
        [FileName] nvarchar(500) NOT NULL,
        [ContentType] nvarchar(200) NOT NULL,
        [FileSize] bigint NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ChildOnboardingEvidence] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChildOnboardingEvidence_ChildOnboardingRequests_RequestId] FOREIGN KEY ([RequestId]) REFERENCES [ChildOnboardingRequests] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE TABLE [ChildOnboardingHistories] (
        [Id] uniqueidentifier NOT NULL,
        [RequestId] uniqueidentifier NOT NULL,
        [Revision] int NOT NULL,
        [ActorApplicationUserId] uniqueidentifier NOT NULL,
        [Action] nvarchar(50) NOT NULL,
        [OccurredAtUtc] datetime2 NOT NULL,
        [Message] nvarchar(2000) NULL,
        CONSTRAINT [PK_ChildOnboardingHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChildOnboardingHistories_ApplicationUsers_ActorApplicationUserId] FOREIGN KEY ([ActorApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ChildOnboardingHistories_ChildOnboardingRequests_RequestId] FOREIGN KEY ([RequestId]) REFERENCES [ChildOnboardingRequests] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE TABLE [OperationalAuthorityHistories] (
        [Id] uniqueidentifier NOT NULL,
        [AuthorityId] uniqueidentifier NOT NULL,
        [ActorApplicationUserId] uniqueidentifier NOT NULL,
        [Action] nvarchar(50) NOT NULL,
        [OccurredAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_OperationalAuthorityHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OperationalAuthorityHistories_ApplicationUsers_ActorApplicationUserId] FOREIGN KEY ([ActorApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OperationalAuthorityHistories_ParentChildOperationalAuthorities_AuthorityId] FOREIGN KEY ([AuthorityId]) REFERENCES [ParentChildOperationalAuthorities] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE INDEX [IX_Patients_DateOfBirth_Gender] ON [Patients] ([DateOfBirth], [Gender]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE INDEX [IX_ChildOnboardingEvidence_RequestId_Revision_Id] ON [ChildOnboardingEvidence] ([RequestId], [Revision], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE INDEX [IX_ChildOnboardingHistories_ActorApplicationUserId] ON [ChildOnboardingHistories] ([ActorApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE INDEX [IX_ChildOnboardingHistories_RequestId_Revision_OccurredAtUtc_Id] ON [ChildOnboardingHistories] ([RequestId], [Revision], [OccurredAtUtc], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ChildOnboardingRequests_ActorApplicationUserId_IdempotencyKey] ON [ChildOnboardingRequests] ([ActorApplicationUserId], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE INDEX [IX_ChildOnboardingRequests_ApprovedPatientId] ON [ChildOnboardingRequests] ([ApprovedPatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE INDEX [IX_ChildOnboardingRequests_FamilyRelationshipRequestId] ON [ChildOnboardingRequests] ([FamilyRelationshipRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ChildOnboardingRequests_ParentPatientId_IntentHash] ON [ChildOnboardingRequests] ([ParentPatientId], [IntentHash]) WHERE [Status] IN (1,2)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE INDEX [IX_ChildOnboardingRequests_ReviewedByApplicationUserId] ON [ChildOnboardingRequests] ([ReviewedByApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE INDEX [IX_ChildOnboardingRequests_Status_CreatedAtUtc_Id] ON [ChildOnboardingRequests] ([Status], [CreatedAtUtc], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE INDEX [IX_FamilyCandidateSelections_ActorApplicationUserId_ExpiresAtUtc] ON [FamilyCandidateSelections] ([ActorApplicationUserId], [ExpiresAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_FamilyCandidateSelections_ActorApplicationUserId_IdempotencyKey] ON [FamilyCandidateSelections] ([ActorApplicationUserId], [IdempotencyKey]) WHERE [IdempotencyKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE INDEX [IX_FamilyCandidateSelections_ParentPatientId] ON [FamilyCandidateSelections] ([ParentPatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE INDEX [IX_FamilyCandidateSelections_RelationshipRequestId] ON [FamilyCandidateSelections] ([RelationshipRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE INDEX [IX_FamilyCandidateSelections_TargetPatientId] ON [FamilyCandidateSelections] ([TargetPatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FamilyCandidateSelections_TokenHash] ON [FamilyCandidateSelections] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE INDEX [IX_OperationalAuthorityHistories_ActorApplicationUserId] ON [OperationalAuthorityHistories] ([ActorApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE INDEX [IX_OperationalAuthorityHistories_AuthorityId_OccurredAtUtc_Id] ON [OperationalAuthorityHistories] ([AuthorityId], [OccurredAtUtc], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ParentChildOperationalAuthorities_ApprovedRelationshipRequestId] ON [ParentChildOperationalAuthorities] ([ApprovedRelationshipRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE INDEX [IX_ParentChildOperationalAuthorities_ChildFamilyMemberId] ON [ParentChildOperationalAuthorities] ([ChildFamilyMemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE INDEX [IX_ParentChildOperationalAuthorities_ChildPatientId] ON [ParentChildOperationalAuthorities] ([ChildPatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE INDEX [IX_ParentChildOperationalAuthorities_ParentFamilyMemberId] ON [ParentChildOperationalAuthorities] ([ParentFamilyMemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE INDEX [IX_ParentChildOperationalAuthorities_ParentPatientId] ON [ParentChildOperationalAuthorities] ([ParentPatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE INDEX [IX_ParentChildOperationalAuthorities_ReviewedByApplicationUserId] ON [ParentChildOperationalAuthorities] ([ReviewedByApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    CREATE INDEX [IX_ParentChildOperationalAuthorities_RevokedByApplicationUserId] ON [ParentChildOperationalAuthorities] ([RevokedByApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_OperationalAuthority_ActivePair] ON [ParentChildOperationalAuthorities] ([FamilyId], [ParentPatientId], [ChildPatientId]) WHERE [IsActive] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107223612_FamilySelfServiceOperationalFoundation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261107223612_FamilySelfServiceOperationalFoundation', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108003144_FamilyDocumentsAndDependentProfile'
)
BEGIN
    CREATE TABLE [DependentProfileChanges] (
        [Id] uniqueidentifier NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [ParentPatientId] uniqueidentifier NOT NULL,
        [ChildPatientId] uniqueidentifier NOT NULL,
        [Operation] nvarchar(30) NOT NULL,
        [IdempotencyKey] nvarchar(100) NOT NULL,
        [Fingerprint] nvarchar(64) NOT NULL,
        [ContactId] uniqueidentifier NULL,
        [OccurredAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_DependentProfileChanges] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DependentProfileChanges_ApplicationUsers_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DependentProfileChanges_PatientContacts_ContactId] FOREIGN KEY ([ContactId]) REFERENCES [PatientContacts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DependentProfileChanges_Patients_ChildPatientId] FOREIGN KEY ([ChildPatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DependentProfileChanges_Patients_ParentPatientId] FOREIGN KEY ([ParentPatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108003144_FamilyDocumentsAndDependentProfile'
)
BEGIN
    CREATE TABLE [PatientHistoricalDocuments] (
        [Id] uniqueidentifier NOT NULL,
        [SubjectPatientId] uniqueidentifier NOT NULL,
        [UploadedByApplicationUserId] uniqueidentifier NOT NULL,
        [UploaderPatientId] uniqueidentifier NOT NULL,
        [UploadedAtUtc] datetime2 NOT NULL,
        [ModifiedAtUtc] datetime2 NOT NULL,
        [CurrentVersionNumber] int NOT NULL,
        [State] int NOT NULL,
        [LegalHold] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PatientHistoricalDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_HistoricalDocument_State] CHECK ([State] IN (1,2,3,4,5)),
        CONSTRAINT [CK_HistoricalDocument_Version] CHECK ([CurrentVersionNumber] >= 0),
        CONSTRAINT [FK_PatientHistoricalDocuments_ApplicationUsers_UploadedByApplicationUserId] FOREIGN KEY ([UploadedByApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientHistoricalDocuments_Patients_SubjectPatientId] FOREIGN KEY ([SubjectPatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientHistoricalDocuments_Patients_UploaderPatientId] FOREIGN KEY ([UploaderPatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108003144_FamilyDocumentsAndDependentProfile'
)
BEGIN
    CREATE TABLE [HistoricalDocumentOperations] (
        [Id] uniqueidentifier NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [SubjectPatientId] uniqueidentifier NOT NULL,
        [Operation] nvarchar(20) NOT NULL,
        [IdempotencyKey] nvarchar(100) NOT NULL,
        [Fingerprint] nvarchar(64) NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [VersionNumber] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_HistoricalDocumentOperations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HistoricalDocumentOperations_ApplicationUsers_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HistoricalDocumentOperations_PatientHistoricalDocuments_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [PatientHistoricalDocuments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HistoricalDocumentOperations_Patients_SubjectPatientId] FOREIGN KEY ([SubjectPatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108003144_FamilyDocumentsAndDependentProfile'
)
BEGIN
    CREATE TABLE [PatientHistoricalDocumentHistories] (
        [Id] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [VersionNumber] int NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [Action] nvarchar(40) NOT NULL,
        [OccurredAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PatientHistoricalDocumentHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PatientHistoricalDocumentHistories_ApplicationUsers_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientHistoricalDocumentHistories_PatientHistoricalDocuments_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [PatientHistoricalDocuments] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108003144_FamilyDocumentsAndDependentProfile'
)
BEGIN
    CREATE TABLE [PatientHistoricalDocumentVersions] (
        [Id] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [VersionNumber] int NOT NULL,
        [Category] int NOT NULL,
        [DocumentDate] date NULL,
        [DateUnknown] bit NOT NULL,
        [ProviderName] nvarchar(200) NULL,
        [Title] nvarchar(200) NULL,
        [PatientNote] nvarchar(2000) NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [Reason] nvarchar(1000) NULL,
        CONSTRAINT [PK_PatientHistoricalDocumentVersions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_HistoricalVersion_Category] CHECK ([Category] IN (1,2,3,4,5)),
        CONSTRAINT [CK_HistoricalVersion_Date] CHECK (([DateUnknown] = 1 AND [DocumentDate] IS NULL) OR ([DateUnknown] = 0 AND [DocumentDate] IS NOT NULL)),
        CONSTRAINT [CK_HistoricalVersion_Number] CHECK ([VersionNumber] > 0),
        CONSTRAINT [FK_PatientHistoricalDocumentVersions_ApplicationUsers_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientHistoricalDocumentVersions_PatientHistoricalDocuments_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [PatientHistoricalDocuments] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108003144_FamilyDocumentsAndDependentProfile'
)
BEGIN
    CREATE TABLE [PatientHistoricalDocumentAttachments] (
        [Id] uniqueidentifier NOT NULL,
        [VersionId] uniqueidentifier NOT NULL,
        [MediaKey] nvarchar(1000) NOT NULL,
        [FileName] nvarchar(500) NOT NULL,
        [ContentType] nvarchar(100) NOT NULL,
        [Length] bigint NOT NULL,
        [Sha256] nvarchar(64) NOT NULL,
        [ScannedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PatientHistoricalDocumentAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_HistoricalAttachment_Length] CHECK ([Length] > 0 AND [Length] <= 10485760),
        CONSTRAINT [FK_PatientHistoricalDocumentAttachments_PatientHistoricalDocumentVersions_VersionId] FOREIGN KEY ([VersionId]) REFERENCES [PatientHistoricalDocumentVersions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108003144_FamilyDocumentsAndDependentProfile'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DependentProfileChanges_ActorUserId_Operation_IdempotencyKey] ON [DependentProfileChanges] ([ActorUserId], [Operation], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108003144_FamilyDocumentsAndDependentProfile'
)
BEGIN
    CREATE INDEX [IX_DependentProfileChanges_ChildPatientId] ON [DependentProfileChanges] ([ChildPatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108003144_FamilyDocumentsAndDependentProfile'
)
BEGIN
    CREATE INDEX [IX_DependentProfileChanges_ContactId] ON [DependentProfileChanges] ([ContactId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108003144_FamilyDocumentsAndDependentProfile'
)
BEGIN
    CREATE INDEX [IX_DependentProfileChanges_ParentPatientId] ON [DependentProfileChanges] ([ParentPatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108003144_FamilyDocumentsAndDependentProfile'
)
BEGIN
    CREATE INDEX [IX_HistoricalDocumentOperations_ActorUserId_CreatedAtUtc] ON [HistoricalDocumentOperations] ([ActorUserId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108003144_FamilyDocumentsAndDependentProfile'
)
BEGIN
    CREATE UNIQUE INDEX [IX_HistoricalDocumentOperations_ActorUserId_Operation_IdempotencyKey] ON [HistoricalDocumentOperations] ([ActorUserId], [Operation], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108003144_FamilyDocumentsAndDependentProfile'
)
BEGIN
    CREATE INDEX [IX_HistoricalDocumentOperations_DocumentId] ON [HistoricalDocumentOperations] ([DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108003144_FamilyDocumentsAndDependentProfile'
)
BEGIN
    CREATE INDEX [IX_HistoricalDocumentOperations_SubjectPatientId] ON [HistoricalDocumentOperations] ([SubjectPatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108003144_FamilyDocumentsAndDependentProfile'
)
BEGIN
    CREATE INDEX [IX_PatientHistoricalDocumentAttachments_VersionId] ON [PatientHistoricalDocumentAttachments] ([VersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108003144_FamilyDocumentsAndDependentProfile'
)
BEGIN
    CREATE INDEX [IX_PatientHistoricalDocumentHistories_ActorUserId] ON [PatientHistoricalDocumentHistories] ([ActorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108003144_FamilyDocumentsAndDependentProfile'
)
BEGIN
    CREATE INDEX [IX_PatientHistoricalDocumentHistories_DocumentId_OccurredAtUtc_Id] ON [PatientHistoricalDocumentHistories] ([DocumentId], [OccurredAtUtc], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108003144_FamilyDocumentsAndDependentProfile'
)
BEGIN
    CREATE INDEX [IX_PatientHistoricalDocuments_SubjectPatientId_State_UploadedAtUtc_Id] ON [PatientHistoricalDocuments] ([SubjectPatientId], [State], [UploadedAtUtc], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108003144_FamilyDocumentsAndDependentProfile'
)
BEGIN
    CREATE INDEX [IX_PatientHistoricalDocuments_UploadedByApplicationUserId] ON [PatientHistoricalDocuments] ([UploadedByApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108003144_FamilyDocumentsAndDependentProfile'
)
BEGIN
    CREATE INDEX [IX_PatientHistoricalDocuments_UploaderPatientId] ON [PatientHistoricalDocuments] ([UploaderPatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108003144_FamilyDocumentsAndDependentProfile'
)
BEGIN
    CREATE INDEX [IX_PatientHistoricalDocumentVersions_CreatedByUserId] ON [PatientHistoricalDocumentVersions] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108003144_FamilyDocumentsAndDependentProfile'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PatientHistoricalDocumentVersions_DocumentId_VersionNumber] ON [PatientHistoricalDocumentVersions] ([DocumentId], [VersionNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108003144_FamilyDocumentsAndDependentProfile'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261108003144_FamilyDocumentsAndDependentProfile', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108004756_HistoricalTimelineAndIntakeRevisions'
)
BEGIN
    ALTER TABLE [PatientHistoricalDocumentVersions] ADD [TimelineAtUtc] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108004756_HistoricalTimelineAndIntakeRevisions'
)
BEGIN
    UPDATE v SET [TimelineAtUtc] = CASE WHEN v.[DateUnknown] = 0 AND v.[DocumentDate] IS NOT NULL THEN CONVERT(datetime2, v.[DocumentDate]) ELSE d.[UploadedAtUtc] END FROM [PatientHistoricalDocumentVersions] v INNER JOIN [PatientHistoricalDocuments] d ON d.[Id] = v.[DocumentId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108004756_HistoricalTimelineAndIntakeRevisions'
)
BEGIN
    CREATE TABLE [ChildOnboardingRevisions] (
        [Id] uniqueidentifier NOT NULL,
        [RequestId] uniqueidentifier NOT NULL,
        [Revision] int NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [NameEn] nvarchar(200) NULL,
        [DateOfBirth] date NOT NULL,
        [Gender] int NOT NULL,
        [PhoneNumber] nvarchar(30) NULL,
        [ContactNameAr] nvarchar(200) NOT NULL,
        [ContactPhone] nvarchar(30) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [ResubmitIdempotencyKey] nvarchar(100) NULL,
        [ResubmitFingerprint] nvarchar(64) NULL,
        [ResultRowVersion] nvarchar(40) NOT NULL,
        CONSTRAINT [PK_ChildOnboardingRevisions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChildOnboardingRevisions_ChildOnboardingRequests_RequestId] FOREIGN KEY ([RequestId]) REFERENCES [ChildOnboardingRequests] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108004756_HistoricalTimelineAndIntakeRevisions'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ChildOnboardingRevisions_RequestId_ResubmitIdempotencyKey] ON [ChildOnboardingRevisions] ([RequestId], [ResubmitIdempotencyKey]) WHERE [ResubmitIdempotencyKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108004756_HistoricalTimelineAndIntakeRevisions'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ChildOnboardingRevisions_RequestId_Revision] ON [ChildOnboardingRevisions] ([RequestId], [Revision]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108004756_HistoricalTimelineAndIntakeRevisions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261108004756_HistoricalTimelineAndIntakeRevisions', N'10.0.10');
END;

COMMIT;
GO


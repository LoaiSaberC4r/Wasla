BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024174818_Phase16ExactFamilyRelationshipProof'
)
BEGIN
    ALTER TABLE [FamilyMedicalEligibilityReviews] ADD [AuthorityRelationshipRequestId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024174818_Phase16ExactFamilyRelationshipProof'
)
BEGIN
    CREATE TABLE [VerifiedParentChildRelationships] (
        [Id] uniqueidentifier NOT NULL,
        [FamilyId] uniqueidentifier NOT NULL,
        [ParentPatientId] uniqueidentifier NOT NULL,
        [ChildPatientId] uniqueidentifier NOT NULL,
        [ParentFamilyMemberId] uniqueidentifier NOT NULL,
        [ChildFamilyMemberId] uniqueidentifier NOT NULL,
        [ParentRole] int NOT NULL,
        [ApprovedRelationshipRequestId] uniqueidentifier NOT NULL,
        [ApprovedRevisionNumber] int NOT NULL,
        [VerifiedBySuperAdminApplicationUserId] uniqueidentifier NOT NULL,
        [VerifiedAtUtc] datetime2 NOT NULL,
        [PolicyVersion] nvarchar(100) NOT NULL,
        [Status] int NOT NULL,
        [RevokedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_VerifiedParentChildRelationships] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_VerifiedParentChild_Distinct] CHECK ([ParentPatientId] <> [ChildPatientId]),
        CONSTRAINT [CK_VerifiedParentChild_Revision] CHECK ([ApprovedRevisionNumber] >= 1),
        CONSTRAINT [CK_VerifiedParentChild_Role] CHECK ([ParentRole] IN (1,2)),
        CONSTRAINT [CK_VerifiedParentChild_Status] CHECK ([Status] IN (1,2)),
        CONSTRAINT [FK_VerifiedParentChildRelationships_ApplicationUsers_VerifiedBySuperAdminApplicationUserId] FOREIGN KEY ([VerifiedBySuperAdminApplicationUserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_VerifiedParentChildRelationships_Families_FamilyId] FOREIGN KEY ([FamilyId]) REFERENCES [Families] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_VerifiedParentChildRelationships_FamilyMembers_ChildFamilyMemberId] FOREIGN KEY ([ChildFamilyMemberId]) REFERENCES [FamilyMembers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_VerifiedParentChildRelationships_FamilyMembers_ParentFamilyMemberId] FOREIGN KEY ([ParentFamilyMemberId]) REFERENCES [FamilyMembers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_VerifiedParentChildRelationships_FamilyRelationshipRequests_ApprovedRelationshipRequestId] FOREIGN KEY ([ApprovedRelationshipRequestId]) REFERENCES [FamilyRelationshipRequests] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_VerifiedParentChildRelationships_Patients_ChildPatientId] FOREIGN KEY ([ChildPatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_VerifiedParentChildRelationships_Patients_ParentPatientId] FOREIGN KEY ([ParentPatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024174818_Phase16ExactFamilyRelationshipProof'
)
BEGIN
    CREATE INDEX [IX_FamilyMedicalEligibilityReviews_AuthorityRelationshipRequestId] ON [FamilyMedicalEligibilityReviews] ([AuthorityRelationshipRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024174818_Phase16ExactFamilyRelationshipProof'
)
BEGIN
    CREATE INDEX [IX_VerifiedParentChildRelationships_ApprovedRelationshipRequestId] ON [VerifiedParentChildRelationships] ([ApprovedRelationshipRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024174818_Phase16ExactFamilyRelationshipProof'
)
BEGIN
    CREATE INDEX [IX_VerifiedParentChildRelationships_ChildFamilyMemberId] ON [VerifiedParentChildRelationships] ([ChildFamilyMemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024174818_Phase16ExactFamilyRelationshipProof'
)
BEGIN
    CREATE INDEX [IX_VerifiedParentChildRelationships_ChildPatientId_FamilyId_Status] ON [VerifiedParentChildRelationships] ([ChildPatientId], [FamilyId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024174818_Phase16ExactFamilyRelationshipProof'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_VerifiedParentChildRelationships_FamilyId_ParentPatientId_ChildPatientId] ON [VerifiedParentChildRelationships] ([FamilyId], [ParentPatientId], [ChildPatientId]) WHERE [Status] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024174818_Phase16ExactFamilyRelationshipProof'
)
BEGIN
    CREATE INDEX [IX_VerifiedParentChildRelationships_ParentFamilyMemberId] ON [VerifiedParentChildRelationships] ([ParentFamilyMemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024174818_Phase16ExactFamilyRelationshipProof'
)
BEGIN
    CREATE INDEX [IX_VerifiedParentChildRelationships_ParentPatientId_FamilyId_Status] ON [VerifiedParentChildRelationships] ([ParentPatientId], [FamilyId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024174818_Phase16ExactFamilyRelationshipProof'
)
BEGIN
    CREATE INDEX [IX_VerifiedParentChildRelationships_VerifiedBySuperAdminApplicationUserId] ON [VerifiedParentChildRelationships] ([VerifiedBySuperAdminApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024174818_Phase16ExactFamilyRelationshipProof'
)
BEGIN
    ALTER TABLE [FamilyMedicalEligibilityReviews] ADD CONSTRAINT [FK_FamilyMedicalEligibilityReviews_FamilyRelationshipRequests_AuthorityRelationshipRequestId] FOREIGN KEY ([AuthorityRelationshipRequestId]) REFERENCES [FamilyRelationshipRequests] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024174818_Phase16ExactFamilyRelationshipProof'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261024174818_Phase16ExactFamilyRelationshipProof', N'10.0.10');
END;

COMMIT;
GO

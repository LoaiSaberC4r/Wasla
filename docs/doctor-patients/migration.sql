BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028111250_DoctorMyPatientsReadIndexes'
)
BEGIN
    DROP INDEX [IX_ArchivedTickets_DoctorId] ON [archive].[Tickets];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028111250_DoctorMyPatientsReadIndexes'
)
BEGIN
    DROP INDEX [IX_Tickets_DoctorId] ON [Tickets];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028111250_DoctorMyPatientsReadIndexes'
)
BEGIN
    DROP INDEX [IX_TicketRegistry_DoctorId] ON [TicketRegistry];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028111250_DoctorMyPatientsReadIndexes'
)
BEGIN
    CREATE INDEX [IX_ArchivedTickets_DoctorId_PatientId_CheckInTimeUtc_Id] ON [archive].[Tickets] ([DoctorId], [PatientId], [CheckInTimeUtc], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028111250_DoctorMyPatientsReadIndexes'
)
BEGIN
    CREATE INDEX [IX_Tickets_DoctorId_PatientId_CheckInTimeUtc_Id] ON [Tickets] ([DoctorId], [PatientId], [CheckInTimeUtc], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028111250_DoctorMyPatientsReadIndexes'
)
BEGIN
    CREATE INDEX [IX_TicketRegistry_DoctorId_PatientId_BusinessDate] ON [TicketRegistry] ([DoctorId], [PatientId], [BusinessDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028111250_DoctorMyPatientsReadIndexes'
)
BEGIN
    CREATE INDEX [IX_ArchivedReservations_DoctorId_PatientId_ScheduledStartUtc] ON [archive].[Reservations] ([DoctorId], [PatientId], [ScheduledStartUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028111250_DoctorMyPatientsReadIndexes'
)
BEGIN
    CREATE INDEX [IX_Reservations_DoctorId_PatientId_ScheduledStartUtc] ON [Reservations] ([DoctorId], [PatientId], [ScheduledStartUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028111250_DoctorMyPatientsReadIndexes'
)
BEGIN
    CREATE INDEX [IX_MedicalEncounters_DoctorId_PatientId_StartedAtUtc] ON [MedicalEncounters] ([DoctorId], [PatientId], [StartedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028111250_DoctorMyPatientsReadIndexes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261028111250_DoctorMyPatientsReadIndexes', N'10.0.10');
END;

COMMIT;
GO


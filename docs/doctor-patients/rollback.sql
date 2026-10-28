BEGIN TRANSACTION;
DROP INDEX [IX_ArchivedTickets_DoctorId_PatientId_CheckInTimeUtc_Id] ON [archive].[Tickets];

DROP INDEX [IX_Tickets_DoctorId_PatientId_CheckInTimeUtc_Id] ON [Tickets];

DROP INDEX [IX_TicketRegistry_DoctorId_PatientId_BusinessDate] ON [TicketRegistry];

DROP INDEX [IX_ArchivedReservations_DoctorId_PatientId_ScheduledStartUtc] ON [archive].[Reservations];

DROP INDEX [IX_Reservations_DoctorId_PatientId_ScheduledStartUtc] ON [Reservations];

DROP INDEX [IX_MedicalEncounters_DoctorId_PatientId_StartedAtUtc] ON [MedicalEncounters];

CREATE INDEX [IX_ArchivedTickets_DoctorId] ON [archive].[Tickets] ([DoctorId]);

CREATE INDEX [IX_Tickets_DoctorId] ON [Tickets] ([DoctorId]);

CREATE INDEX [IX_TicketRegistry_DoctorId] ON [TicketRegistry] ([DoctorId]);

DELETE FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20261028111250_DoctorMyPatientsReadIndexes';

COMMIT;
GO


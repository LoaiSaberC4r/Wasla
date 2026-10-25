using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861 // EF-generated one-time migration column arrays.
#nullable disable

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase17NotificationsAndReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(name: "LastAttemptOnUtc", table: "EmailOutboxMessages", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<Guid>(
                name: "AppointmentRevisionHistoryId",
                schema: "archive",
                table: "Reservations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AppointmentRevisionHistoryId",
                table: "Reservations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NotificationDeliveryId",
                table: "EmailOutboxMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NotificationAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OutcomeCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceEventKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceRevisionKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    DataClassification = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LegacyEmailOwned = table.Column<bool>(type: "bit", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    PreferredLanguage = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationPreferences_ApplicationUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlatformNotificationSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentReminderEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ReminderLeadMinutes = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformNotificationSettings", x => x.Id);
                    table.CheckConstraint("CK_NotificationSettings_Lead", "[ReminderLeadMinutes] BETWEEN 1 AND 43200");
                    table.CheckConstraint("CK_NotificationSettings_Singleton", "[Id] = '17000000-0000-0000-0000-000000000001'");
                });

            migrationBuilder.CreateTable(
                name: "PlatformNotificationSettingsAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousVersion = table.Column<long>(type: "bigint", nullable: false),
                    NewVersion = table.Column<long>(type: "bigint", nullable: false),
                    PreviousReminderLeadMinutes = table.Column<int>(type: "int", nullable: false),
                    NewReminderLeadMinutes = table.Column<int>(type: "int", nullable: false),
                    PreviousEnabled = table.Column<bool>(type: "bit", nullable: false),
                    NewEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformNotificationSettingsAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationIntents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SourceRevisionKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AppointmentStartUtcSnapshot = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReminderLeadMinutesSnapshot = table.Column<int>(type: "int", nullable: true),
                    SettingsVersionSnapshot = table.Column<long>(type: "bigint", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessingToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SkipReasonCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationIntents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationIntents_NotificationEvents_NotificationEventId",
                        column: x => x.NotificationEventId,
                        principalTable: "NotificationEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationIntentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RecipientLogicalKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecipientApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecipientPatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TemplateCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TemplateVersion = table.Column<int>(type: "int", nullable: false),
                    LogicalDeliveryKey = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DispatchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SafeFailureCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationDeliveries_NotificationEvents_NotificationEventId",
                        column: x => x.NotificationEventId,
                        principalTable: "NotificationEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotificationDeliveries_NotificationIntents_NotificationIntentId",
                        column: x => x.NotificationIntentId,
                        principalTable: "NotificationIntents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationDeliveryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TemplateCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TemplateVersion = table.Column<int>(type: "int", nullable: false),
                    SafePreviewCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TargetResourceType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TargetResourceKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserNotifications_ApplicationUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserNotifications_NotificationDeliveries_NotificationDeliveryId",
                        column: x => x.NotificationDeliveryId,
                        principalTable: "NotificationDeliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "PlatformNotificationSettings",
                columns: new[] { "Id", "AppointmentReminderEnabled", "ReminderLeadMinutes", "UpdatedAtUtc", "UpdatedByApplicationUserId", "Version" },
                values: new object[] { new Guid("17000000-0000-0000-0000-000000000001"), true, 120, new DateTime(2026, 10, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, 1L });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_AppointmentRevisionHistoryId",
                table: "Reservations",
                column: "AppointmentRevisionHistoryId",
                unique: true,
                filter: "[AppointmentRevisionHistoryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutboxMessages_NotificationDeliveryId",
                table: "EmailOutboxMessages",
                column: "NotificationDeliveryId",
                unique: true,
                filter: "[NotificationDeliveryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationAudits_OccurredAtUtc",
                table: "NotificationAudits",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_LogicalDeliveryKey",
                table: "NotificationDeliveries",
                column: "LogicalDeliveryKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_NotificationEventId",
                table: "NotificationDeliveries",
                column: "NotificationEventId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_NotificationIntentId",
                table: "NotificationDeliveries",
                column: "NotificationIntentId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_Status_CreatedAtUtc",
                table: "NotificationDeliveries",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationEvents_IdempotencyKey",
                table: "NotificationEvents",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationEvents_SourceType_SourceId_SourceRevisionKey",
                table: "NotificationEvents",
                columns: new[] { "SourceType", "SourceId", "SourceRevisionKey" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationIntents_NotificationEventId",
                table: "NotificationIntents",
                column: "NotificationEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationIntents_Status_DueAtUtc",
                table: "NotificationIntents",
                columns: new[] { "Status", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationIntents_Status_NextAttemptOnUtc",
                table: "NotificationIntents",
                columns: new[] { "Status", "NextAttemptOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_ApplicationUserId_Category_Channel",
                table: "NotificationPreferences",
                columns: new[] { "ApplicationUserId", "Category", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformNotificationSettingsAudits_NewVersion",
                table: "PlatformNotificationSettingsAudits",
                column: "NewVersion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_ApplicationUserId_CreatedAtUtc_Id",
                table: "UserNotifications",
                columns: new[] { "ApplicationUserId", "CreatedAtUtc", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_ApplicationUserId_ReadAtUtc_CreatedAtUtc",
                table: "UserNotifications",
                columns: new[] { "ApplicationUserId", "ReadAtUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_NotificationDeliveryId_ApplicationUserId",
                table: "UserNotifications",
                columns: new[] { "NotificationDeliveryId", "ApplicationUserId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EmailOutboxMessages_NotificationDeliveries_NotificationDeliveryId",
                table: "EmailOutboxMessages",
                column: "NotificationDeliveryId",
                principalTable: "NotificationDeliveries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "LastAttemptOnUtc", table: "EmailOutboxMessages");
            migrationBuilder.DropForeignKey(
                name: "FK_EmailOutboxMessages_NotificationDeliveries_NotificationDeliveryId",
                table: "EmailOutboxMessages");

            migrationBuilder.DropTable(
                name: "NotificationAudits");

            migrationBuilder.DropTable(
                name: "NotificationPreferences");

            migrationBuilder.DropTable(
                name: "PlatformNotificationSettings");

            migrationBuilder.DropTable(
                name: "PlatformNotificationSettingsAudits");

            migrationBuilder.DropTable(
                name: "UserNotifications");

            migrationBuilder.DropTable(
                name: "NotificationDeliveries");

            migrationBuilder.DropTable(
                name: "NotificationIntents");

            migrationBuilder.DropTable(
                name: "NotificationEvents");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_AppointmentRevisionHistoryId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_EmailOutboxMessages_NotificationDeliveryId",
                table: "EmailOutboxMessages");

            migrationBuilder.DropColumn(
                name: "AppointmentRevisionHistoryId",
                schema: "archive",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "AppointmentRevisionHistoryId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "NotificationDeliveryId",
                table: "EmailOutboxMessages");
        }
    }
}

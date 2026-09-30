using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase12PaymentsRefundsRevenue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_DoctorId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_DoctorPracticeId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_PatientId",
                table: "Payments");

            migrationBuilder.AddColumn<DateOnly>(
                name: "BusinessDate",
                table: "Payments",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "Payments",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "EGP");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Payments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentMethod",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNumber",
                table: "Payments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Payments",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: Array.Empty<byte>());

            migrationBuilder.AddColumn<int>(
                name: "SequenceNumber",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TransactionNumber",
                table: "Payments",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "FinancialDailyCounters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TransactionType = table.Column<int>(type: "int", nullable: false),
                    LastNumber = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialDailyCounters", x => x.Id);
                    table.CheckConstraint("CK_FinancialDailyCounters_LastNumber", "[LastNumber] >= 0");
                    table.CheckConstraint("CK_FinancialDailyCounters_Type", "[TransactionType] IN (1,2)");
                    table.ForeignKey(
                        name: "FK_FinancialDailyCounters_DoctorPractices_DoctorPracticeId",
                        column: x => x.DoctorPracticeId,
                        principalTable: "DoctorPractices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinancialIdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CompletedOnUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialIdempotencyRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialIdempotencyRecords_ApplicationUsers_ActorApplicationUserId",
                        column: x => x.ActorApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentCorrectionHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OldPaymentMethod = table.Column<int>(type: "int", nullable: false),
                    NewPaymentMethod = table.Column<int>(type: "int", nullable: false),
                    OldReferenceNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NewReferenceNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OldNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NewNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CorrectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CorrectedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrectedOnUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentCorrectionHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentCorrectionHistories_ApplicationUsers_CorrectedByApplicationUserId",
                        column: x => x.CorrectedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentCorrectionHistories_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Refunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    RefundMethod = table.Column<int>(type: "int", nullable: false),
                    RefundReasonCode = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RefundedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RefundedOnUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.Id);
                    table.CheckConstraint("CK_Refunds_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_Refunds_CurrencyCode", "[CurrencyCode] = 'EGP'");
                    table.CheckConstraint("CK_Refunds_Method", "[RefundMethod] IN (1,2,3)");
                    table.CheckConstraint("CK_Refunds_OtherReason", "[RefundReasonCode] <> 6 OR NULLIF(LTRIM(RTRIM([Reason])), '') IS NOT NULL");
                    table.CheckConstraint("CK_Refunds_ReasonCode", "[RefundReasonCode] IN (1,2,3,4,5,6)");
                    table.CheckConstraint("CK_Refunds_SequenceNumber", "[SequenceNumber] > 0");
                    table.ForeignKey(
                        name: "FK_Refunds_ApplicationUsers_RefundedByApplicationUserId",
                        column: x => x.RefundedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_DoctorPractices_DoctorPracticeId",
                        column: x => x.DoctorPracticeId,
                        principalTable: "DoctorPractices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefundCorrectionHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RefundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OldRefundMethod = table.Column<int>(type: "int", nullable: false),
                    NewRefundMethod = table.Column<int>(type: "int", nullable: false),
                    OldRefundReasonCode = table.Column<int>(type: "int", nullable: false),
                    NewRefundReasonCode = table.Column<int>(type: "int", nullable: false),
                    OldReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NewReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OldReferenceNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NewReferenceNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OldNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NewNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CorrectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CorrectedByApplicationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrectedOnUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefundCorrectionHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefundCorrectionHistories_ApplicationUsers_CorrectedByApplicationUserId",
                        column: x => x.CorrectedByApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RefundCorrectionHistories_Refunds_RefundId",
                        column: x => x.RefundId,
                        principalTable: "Refunds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Phase 11 recorded the collection but did not capture a method or financial
            // number. Preserve unknown method as LegacyUnspecified (0). The linked Ticket
            // supplies the historical practice business date. Ordering by time and Id makes
            // the allocated legacy sequence deterministic, and the counter seed ensures
            // new transactions continue after those rows.
            migrationBuilder.Sql("""
                ;WITH LegacyPayments AS
                (
                    SELECT p.[Id], t.[BusinessDate],
                           ROW_NUMBER() OVER
                           (
                               PARTITION BY p.[DoctorPracticeId], t.[BusinessDate]
                               ORDER BY p.[CollectedOnUtc], p.[Id]
                           ) AS [AllocatedSequence]
                    FROM [Payments] AS p
                    INNER JOIN [Tickets] AS t ON t.[Id] = p.[TicketId]
                )
                UPDATE p
                SET p.[BusinessDate] = legacy.[BusinessDate],
                    p.[CurrencyCode] = 'EGP',
                    p.[SequenceNumber] = CONVERT(int, legacy.[AllocatedSequence]),
                    p.[TransactionNumber] =
                        'PAY-' + REPLACE(CONVERT(char(36), p.[DoctorPracticeId]), '-', '') +
                        '-' + CONVERT(char(8), legacy.[BusinessDate], 112) + '-' +
                        CASE WHEN legacy.[AllocatedSequence] < 1000000
                             THEN RIGHT('000000' + CONVERT(varchar(20), legacy.[AllocatedSequence]), 6)
                             ELSE CONVERT(varchar(20), legacy.[AllocatedSequence]) END
                FROM [Payments] AS p
                INNER JOIN LegacyPayments AS legacy ON legacy.[Id] = p.[Id];

                INSERT INTO [FinancialDailyCounters]
                    ([Id], [DoctorPracticeId], [BusinessDate], [TransactionType], [LastNumber])
                SELECT NEWID(), [DoctorPracticeId], [BusinessDate], 1, MAX([SequenceNumber])
                FROM [Payments]
                GROUP BY [DoctorPracticeId], [BusinessDate];
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_DoctorId_BusinessDate",
                table: "Payments",
                columns: new[] { "DoctorId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_DoctorPracticeId_BusinessDate",
                table: "Payments",
                columns: new[] { "DoctorPracticeId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PatientId_BusinessDate",
                table: "Payments",
                columns: new[] { "PatientId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentMethod_BusinessDate",
                table: "Payments",
                columns: new[] { "PaymentMethod", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "UX_Payments_TransactionNumber",
                table: "Payments",
                column: "TransactionNumber",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_CurrencyCode",
                table: "Payments",
                sql: "[CurrencyCode] = 'EGP'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Method",
                table: "Payments",
                sql: "[PaymentMethod] IN (0,1,2,3)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_SequenceNumber",
                table: "Payments",
                sql: "[SequenceNumber] > 0");

            migrationBuilder.CreateIndex(
                name: "UX_FinancialDailyCounters_PracticeDateType",
                table: "FinancialDailyCounters",
                columns: new[] { "DoctorPracticeId", "BusinessDate", "TransactionType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_FinancialIdempotency_ActorOperationKey",
                table: "FinancialIdempotencyRecords",
                columns: new[] { "ActorApplicationUserId", "Operation", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentCorrectionHistories_CorrectedByApplicationUserId",
                table: "PaymentCorrectionHistories",
                column: "CorrectedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentCorrectionHistories_PaymentId_CorrectedOnUtc",
                table: "PaymentCorrectionHistories",
                columns: new[] { "PaymentId", "CorrectedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RefundCorrectionHistories_CorrectedByApplicationUserId",
                table: "RefundCorrectionHistories",
                column: "CorrectedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RefundCorrectionHistories_RefundId_CorrectedOnUtc",
                table: "RefundCorrectionHistories",
                columns: new[] { "RefundId", "CorrectedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_DoctorId_BusinessDate",
                table: "Refunds",
                columns: new[] { "DoctorId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_DoctorPracticeId_BusinessDate",
                table: "Refunds",
                columns: new[] { "DoctorPracticeId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_PatientId_BusinessDate",
                table: "Refunds",
                columns: new[] { "PatientId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_RefundedByApplicationUserId",
                table: "Refunds",
                column: "RefundedByApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_RefundMethod_BusinessDate",
                table: "Refunds",
                columns: new[] { "RefundMethod", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_TicketId",
                table: "Refunds",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "UX_Refunds_PaymentId",
                table: "Refunds",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Refunds_TransactionNumber",
                table: "Refunds",
                column: "TransactionNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinancialDailyCounters");

            migrationBuilder.DropTable(
                name: "FinancialIdempotencyRecords");

            migrationBuilder.DropTable(
                name: "PaymentCorrectionHistories");

            migrationBuilder.DropTable(
                name: "RefundCorrectionHistories");

            migrationBuilder.DropTable(
                name: "Refunds");

            migrationBuilder.DropIndex(
                name: "IX_Payments_DoctorId_BusinessDate",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_DoctorPracticeId_BusinessDate",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_PatientId_BusinessDate",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_PaymentMethod_BusinessDate",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "UX_Payments_TransactionNumber",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_CurrencyCode",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Method",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_SequenceNumber",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "BusinessDate",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReferenceNumber",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "SequenceNumber",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "TransactionNumber",
                table: "Payments");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_DoctorId",
                table: "Payments",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_DoctorPracticeId",
                table: "Payments",
                column: "DoctorPracticeId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PatientId",
                table: "Payments",
                column: "PatientId");
        }
    }
}

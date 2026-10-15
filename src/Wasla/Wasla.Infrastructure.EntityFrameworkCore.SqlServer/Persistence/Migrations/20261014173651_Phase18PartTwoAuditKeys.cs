using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase18PartTwoAuditKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "EventKeySecret",
                table: "ArchiveControlStates",
                type: "varbinary(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: Array.Empty<byte>());
            migrationBuilder.Sql("UPDATE dbo.ArchiveControlStates SET EventKeySecret=CRYPT_GEN_RANDOM(32) WHERE DATALENGTH(EventKeySecret)<>32;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Stable audit key reversal requires an approved recovery plan.");
        }
    }
}

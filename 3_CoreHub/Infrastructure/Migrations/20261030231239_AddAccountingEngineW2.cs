using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountingEngineW2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MappingVersion",
                table: "JournalEntries",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AllocatedToEntryId",
                table: "AccountingEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CashAccountCode",
                table: "AccountingEntries",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "AccountingEntries",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoucherNumber",
                table: "AccountingEntries",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveFrom",
                table: "AccountCharts",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentCode",
                table: "AccountCharts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccountMappingRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Regime = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    EventCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DebitAccount = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreditAccount = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AmountKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountMappingRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UploadedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantAccountCharts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AccountName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: true),
                    IsNormalCredit = table.Column<bool>(type: "boolean", nullable: true),
                    ParentCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Source = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantAccountCharts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VoucherSequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    LastNumber = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherSequences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntries_TenantId_VoucherNumber",
                table: "AccountingEntries",
                columns: new[] { "TenantId", "VoucherNumber" },
                unique: true,
                filter: "\"VoucherNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountMappingRules_Regime_EventCode",
                table: "AccountMappingRules",
                columns: new[] { "Regime", "EventCode" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountMappingRules_TenantId_EventCode",
                table: "AccountMappingRules",
                columns: new[] { "TenantId", "EventCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_TenantId_EntityType_EntityId",
                table: "Attachments",
                columns: new[] { "TenantId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantAccountCharts_TenantId_AccountCode",
                table: "TenantAccountCharts",
                columns: new[] { "TenantId", "AccountCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VoucherSequences_TenantId_VoucherType_Year",
                table: "VoucherSequences",
                columns: new[] { "TenantId", "VoucherType", "Year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountMappingRules");

            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "TenantAccountCharts");

            migrationBuilder.DropTable(
                name: "VoucherSequences");

            migrationBuilder.DropIndex(
                name: "IX_AccountingEntries_TenantId_VoucherNumber",
                table: "AccountingEntries");

            migrationBuilder.DropColumn(
                name: "MappingVersion",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "AllocatedToEntryId",
                table: "AccountingEntries");

            migrationBuilder.DropColumn(
                name: "CashAccountCode",
                table: "AccountingEntries");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "AccountingEntries");

            migrationBuilder.DropColumn(
                name: "VoucherNumber",
                table: "AccountingEntries");

            migrationBuilder.DropColumn(
                name: "EffectiveFrom",
                table: "AccountCharts");

            migrationBuilder.DropColumn(
                name: "ParentCode",
                table: "AccountCharts");
        }
    }
}

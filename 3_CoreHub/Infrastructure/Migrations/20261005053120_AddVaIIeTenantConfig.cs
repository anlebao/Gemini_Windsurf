using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVaIIeTenantConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VaIIeTenantConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AvgDailyConsumptionWindowDays = table.Column<int>(type: "integer", nullable: false, defaultValue: 14),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    SafetyDays = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    TelegramEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    TelegramBotToken = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    TelegramChatId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ZaloEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ZaloAccessToken = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ZaloRecipientId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaIIeTenantConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VaIIeTenantConfigs_TenantId",
                table: "VaIIeTenantConfigs",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VaIIeTenantConfigs");
        }
    }
}

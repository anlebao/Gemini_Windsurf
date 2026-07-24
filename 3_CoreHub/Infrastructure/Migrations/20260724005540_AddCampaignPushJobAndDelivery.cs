using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignPushJobAndDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Settings_Theme",
                table: "Tenants",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CampaignPushJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    CriteriaJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    SentCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    FailedCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ClickedCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignPushJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PushNotificationDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignPushJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Delivered"),
                    ClickedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ActionUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushNotificationDeliveries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPushJobs_CampaignId",
                table: "CampaignPushJobs",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPushJobs_TenantId_CampaignId",
                table: "CampaignPushJobs",
                columns: new[] { "TenantId", "CampaignId" });

            migrationBuilder.CreateIndex(
                name: "IX_PushNotificationDeliveries_CampaignPushJobId",
                table: "PushNotificationDeliveries",
                column: "CampaignPushJobId");

            migrationBuilder.CreateIndex(
                name: "IX_PushNotificationDeliveries_CustomerId",
                table: "PushNotificationDeliveries",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PushNotificationDeliveries_NotificationId",
                table: "PushNotificationDeliveries",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_PushNotificationDeliveries_TenantId_CustomerId",
                table: "PushNotificationDeliveries",
                columns: new[] { "TenantId", "CustomerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignPushJobs");

            migrationBuilder.DropTable(
                name: "PushNotificationDeliveries");

            migrationBuilder.AlterColumn<int>(
                name: "Settings_Theme",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}

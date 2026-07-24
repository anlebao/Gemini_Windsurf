using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.ShopERP.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignPushJobAndDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampaignPushJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CriteriaJson = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    SentCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    FailedCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    ClickedCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignPushJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PushNotificationDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignPushJobId = table.Column<Guid>(type: "TEXT", nullable: true),
                    NotificationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Delivered"),
                    ClickedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActionUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
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
        }
    }
}

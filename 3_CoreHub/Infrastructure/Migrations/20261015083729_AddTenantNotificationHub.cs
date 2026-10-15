using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantNotificationHub : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Body",
                table: "PushNotificationDeliveries",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "PushNotificationDeliveries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "PushNotificationDeliveries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAt",
                table: "PushNotificationDeliveries",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferenceId",
                table: "PushNotificationDeliveries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceType",
                table: "PushNotificationDeliveries",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "PushNotificationDeliveries",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TenantNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ActionUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReferenceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Severity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantNotifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantNotifications_ReferenceType_ReferenceId",
                table: "TenantNotifications",
                columns: new[] { "ReferenceType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantNotifications_TenantId_Category_IsRead",
                table: "TenantNotifications",
                columns: new[] { "TenantId", "Category", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantNotifications_TenantId_CreatedAt",
                table: "TenantNotifications",
                columns: new[] { "TenantId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantNotifications");

            migrationBuilder.DropColumn(
                name: "Body",
                table: "PushNotificationDeliveries");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "PushNotificationDeliveries");

            migrationBuilder.DropColumn(
                name: "IsRead",
                table: "PushNotificationDeliveries");

            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "PushNotificationDeliveries");

            migrationBuilder.DropColumn(
                name: "ReferenceId",
                table: "PushNotificationDeliveries");

            migrationBuilder.DropColumn(
                name: "ReferenceType",
                table: "PushNotificationDeliveries");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "PushNotificationDeliveries");
        }
    }
}

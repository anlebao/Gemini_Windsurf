using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKhachLinkInstances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // KhachLink Multi-Profile R1: KhachLinkInstances table (platform-level routing entity)
            // Note: Pre-existing datetime type drift (timestamp with time zone → without time zone)
            // is NOT included in this migration — it's a separate concern to be addressed independently.
            migrationBuilder.CreateTable(
                name: "KhachLinkInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Profile = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CustomDomain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    OwnerTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    NavFlags_ShowHome = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NavFlags_ShowCart = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NavFlags_ShowOrders = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NavFlags_ShowLoyaltyHistory = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NavFlags_ShowMissions = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NavFlags_ShowRewards = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NavFlags_ShowAllianceWallet = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NavFlags_ShowStores = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NavFlags_ShowCampaigns = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NavFlags_ShowScan = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NavFlags_ShowQrClaim = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NavFlags_ShowCommunity = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NavFlags_ShowJobs = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    NavFlags_ShowProfile = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NavFlags_ShowStaffDashboard = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KhachLinkInstances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KhachLinkInstances_CustomDomain",
                table: "KhachLinkInstances",
                column: "CustomDomain",
                unique: true);

            // Seed: existing KhachLink deployment (diemthuong2.khachvip.online) → FullCommerce instance
            // Fixed seed GUID for idempotency
            migrationBuilder.InsertData(
                table: "KhachLinkInstances",
                columns: new[] { "Id", "Label", "Profile", "CustomDomain", "OwnerTenantId", "IsActive", "TenantId", "CreatedAt", "UpdatedAt", "IsDeleted",
                    "NavFlags_ShowHome", "NavFlags_ShowCart", "NavFlags_ShowOrders", "NavFlags_ShowLoyaltyHistory", "NavFlags_ShowMissions",
                    "NavFlags_ShowRewards", "NavFlags_ShowAllianceWallet", "NavFlags_ShowStores", "NavFlags_ShowCampaigns",
                    "NavFlags_ShowScan", "NavFlags_ShowQrClaim", "NavFlags_ShowCommunity", "NavFlags_ShowJobs",
                    "NavFlags_ShowProfile", "NavFlags_ShowStaffDashboard" },
                columnTypes: new[] { "uuid", "text", "integer", "text", "uuid", "boolean", "uuid", "timestamp without time zone", "timestamp without time zone", "boolean",
                    "boolean", "boolean", "boolean", "boolean", "boolean", "boolean", "boolean", "boolean", "boolean", "boolean", "boolean", "boolean", "boolean", "boolean", "boolean" },
                values: new object[] {
                    new Guid("01910000-0000-0000-0000-000000000001"),
                    "KhachLink Default (FullCommerce)",
                    0,  // FullCommerce
                    "diemthuong2.khachvip.online",
                    null,  // platform-level (no owner tenant)
                    true,
                    Guid.Empty,  // platform sentinel
                    new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
                    false,
                    // NavFlags — FullCommerce preset (all true except ShowJobs)
                    true, true, true, true, true, true, true, true, true, true, true, true, false, true, true
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove seed data first
            migrationBuilder.DeleteData(
                table: "KhachLinkInstances",
                keyColumn: "Id",
                keyValue: new Guid("01910000-0000-0000-0000-000000000001"));

            migrationBuilder.DropTable(
                name: "KhachLinkInstances");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.ShopERP.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// KL W0: Add ShopFeatureSettings table for module toggle infrastructure.
    /// Manual migration — only creates/drops ShopFeatureSettings table.
    /// Does NOT touch accounting tables (moved to PostgreSQL per ADR-001).
    /// </summary>
    public partial class AddShopFeatureSettingsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShopFeatureSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    QR_TableNumber_Enabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    Kitchen_Workflow_Enabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    Voice_Note_Enabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    Loyalty_Program_Enabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    Accounting_Sync_Enabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    EInvoice_Auto_Export_Enabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopFeatureSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShopFeatureSettings_TenantId",
                table: "ShopFeatureSettings",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShopFeatureSettings");
        }
    }
}

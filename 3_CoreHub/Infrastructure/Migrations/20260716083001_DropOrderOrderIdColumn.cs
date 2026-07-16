using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropOrderOrderIdColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "Orders");

            migrationBuilder.CreateTable(
                name: "ShopFeatureSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QR_TableNumber_Enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Kitchen_Workflow_Enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Voice_Note_Enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Loyalty_Program_Enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Accounting_Sync_Enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    EInvoice_Auto_Export_Enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PollingIntervalSeconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 15),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
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

            migrationBuilder.AddColumn<Guid>(
                name: "OrderId",
                table: "Orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }
    }
}

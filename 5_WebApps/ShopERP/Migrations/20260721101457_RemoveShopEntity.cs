using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.ShopERP.Migrations
{
    /// <inheritdoc />
    public partial class RemoveShopEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DATA PRESERVATION (2026-07-21): Copy Shop lat/lng → Tenant.Settings BEFORE dropping Shops table.
            migrationBuilder.AddColumn<double>(
                name: "Settings_Latitude",
                table: "Tenants",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Settings_Longitude",
                table: "Tenants",
                type: "REAL",
                nullable: true);

            // SQLite syntax for copying lat/lng from Shops to Tenants.
            migrationBuilder.Sql(@"
                UPDATE Tenants
                SET Settings_Latitude = (
                        SELECT Latitude FROM Shops
                        WHERE Shops.TenantId = Tenants.Id
                          AND Shops.Latitude IS NOT NULL
                        LIMIT 1
                    ),
                    Settings_Longitude = (
                        SELECT Longitude FROM Shops
                        WHERE Shops.TenantId = Tenants.Id
                          AND Shops.Longitude IS NOT NULL
                        LIMIT 1
                    )
                WHERE EXISTS (SELECT 1 FROM Shops WHERE Shops.TenantId = Tenants.Id);
            ");

            // Null out all campaign ShopIds so they become tenant-wide (no FK violation when Shops dropped).
            migrationBuilder.Sql("UPDATE SocialCampaigns SET ShopId = NULL WHERE ShopId IS NOT NULL;");

            // Now safe to drop the FK + table + column.
            migrationBuilder.DropForeignKey(
                name: "FK_SocialCampaigns_Shops_ShopId",
                table: "SocialCampaigns");

            migrationBuilder.DropTable(
                name: "Shops");

            migrationBuilder.DropIndex(
                name: "IX_SocialCampaigns_ShopId",
                table: "SocialCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_SocialCampaigns_TenantId_ShopId",
                table: "SocialCampaigns");

            migrationBuilder.DropColumn(
                name: "ShopId",
                table: "SocialCampaigns");

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "SocialCampaigns",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoUrl",
                table: "SocialCampaigns",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                table: "FeaturedProducts",
                type: "TEXT",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0.10m);

            migrationBuilder.CreateIndex(
                name: "IX_SocialCampaigns_TenantId",
                table: "SocialCampaigns",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SocialCampaigns_TenantId",
                table: "SocialCampaigns");

            migrationBuilder.DropColumn(
                name: "Settings_Latitude",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Settings_Longitude",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "SocialCampaigns");

            migrationBuilder.DropColumn(
                name: "VideoUrl",
                table: "SocialCampaigns");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "FeaturedProducts");

            migrationBuilder.AddColumn<Guid>(
                name: "ShopId",
                table: "SocialCampaigns",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Shops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: true),
                    Longitude = table.Column<double>(type: "REAL", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shops", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SocialCampaigns_ShopId",
                table: "SocialCampaigns",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_SocialCampaigns_TenantId_ShopId",
                table: "SocialCampaigns",
                columns: new[] { "TenantId", "ShopId" });

            migrationBuilder.CreateIndex(
                name: "IX_Shops_TenantId_Name",
                table: "Shops",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_SocialCampaigns_Shops_ShopId",
                table: "SocialCampaigns",
                column: "ShopId",
                principalTable: "Shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.ShopERP.Migrations
{
    /// <inheritdoc />
    public partial class AddCommerceModeSprint7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Settings_CommerceModeOverride",
                table: "Tenants",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CommerceMode",
                table: "Orders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CommunityFundRate",
                table: "Orders",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostPrice",
                table: "Orders",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryFee",
                table: "Orders",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFeeRate",
                table: "Orders",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformMargin",
                table: "Orders",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SellPrice",
                table: "Orders",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CommunityFundSpendRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Recipient = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    SpentAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    WalletTransactionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityFundSpendRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductCostPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CostPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCostPrices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CommerceMode",
                table: "Orders",
                column: "CommerceMode");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityFundSpendRecords_SpentAt",
                table: "CommunityFundSpendRecords",
                column: "SpentAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCostPrices_TenantId_ProductId",
                table: "ProductCostPrices",
                columns: new[] { "TenantId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Key",
                table: "SystemSettings",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityFundSpendRecords");

            migrationBuilder.DropTable(
                name: "ProductCostPrices");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropIndex(
                name: "IX_Orders_CommerceMode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Settings_CommerceModeOverride",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CommerceMode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CommunityFundRate",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CostPrice",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryFee",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PlatformFeeRate",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PlatformMargin",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SellPrice",
                table: "Orders");
        }
    }
}

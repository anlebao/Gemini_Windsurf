using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.ShopERP.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSettingsB09DNAndStyleColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Settings_BusinessField",
                table: "Tenants",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Settings_CharterCapital",
                table: "Tenants",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Settings_FooterColor",
                table: "Tenants",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Settings_HeaderColor",
                table: "Tenants",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Settings_LegalForm",
                table: "Tenants",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Settings_NavColor",
                table: "Tenants",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Home_CampaignSection_Enabled",
                table: "ShopFeatureSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Home_FeaturedSection_Enabled",
                table: "ShopFeatureSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Home_SocialHub_Enabled",
                table: "ShopFeatureSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Home_StoreSection_Enabled",
                table: "ShopFeatureSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AllianceTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WalletId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TransactionTenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false),
                    BalanceAfter = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SourceOrderId = table.Column<Guid>(type: "TEXT", nullable: true),
                    VoucherCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    RefundTenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TransactionAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllianceTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AllianceWallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerDeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    TotalPointBalance = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastEarnAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastRedeemAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllianceWallets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyGlobalConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    PointsRate = table.Column<int>(type: "INTEGER", nullable: false),
                    MinPointsPerOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxPointsPerOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxWalletPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    LastChangedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastChangedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyGlobalConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyTenantConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Mode = table.Column<int>(type: "INTEGER", nullable: true),
                    IsAllianceMember = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxWalletPoints = table.Column<int>(type: "INTEGER", nullable: true),
                    LastChangedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastChangedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyTenantConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AllianceTransactions_IdempotencyKey",
                table: "AllianceTransactions",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_AllianceTransactions_WalletId_TransactionAt",
                table: "AllianceTransactions",
                columns: new[] { "WalletId", "TransactionAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AllianceWallets_CustomerDeviceId",
                table: "AllianceWallets",
                column: "CustomerDeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTenantConfigs_TenantId",
                table: "LoyaltyTenantConfigs",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AllianceTransactions");

            migrationBuilder.DropTable(
                name: "AllianceWallets");

            migrationBuilder.DropTable(
                name: "LoyaltyGlobalConfigs");

            migrationBuilder.DropTable(
                name: "LoyaltyTenantConfigs");

            migrationBuilder.DropColumn(
                name: "Settings_BusinessField",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Settings_CharterCapital",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Settings_FooterColor",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Settings_HeaderColor",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Settings_LegalForm",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Settings_NavColor",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Home_CampaignSection_Enabled",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Home_FeaturedSection_Enabled",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Home_SocialHub_Enabled",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Home_StoreSection_Enabled",
                table: "ShopFeatureSettings");
        }
    }
}

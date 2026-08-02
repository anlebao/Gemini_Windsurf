using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LoyaltyAlliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AllianceTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    BalanceAfter = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    VoucherCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RefundTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransactionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllianceTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AllianceWallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerDeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TotalPointBalance = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastEarnAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastRedeemAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllianceWallets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyGlobalConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    PointsRate = table.Column<int>(type: "integer", nullable: false),
                    MinPointsPerOrder = table.Column<int>(type: "integer", nullable: false),
                    MaxPointsPerOrder = table.Column<int>(type: "integer", nullable: false),
                    MaxWalletPoints = table.Column<int>(type: "integer", nullable: false),
                    LastChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastChangedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyGlobalConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyTenantConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: true),
                    IsAllianceMember = table.Column<bool>(type: "boolean", nullable: false),
                    MaxWalletPoints = table.Column<int>(type: "integer", nullable: true),
                    LastChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastChangedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyTenantConfigs", x => x.Id);
                });

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
        }
    }
}

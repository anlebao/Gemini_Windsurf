using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Migrations
{
    /// <inheritdoc />
    public partial class AddValcnV2PlatformLightFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFeeRate",
                table: "ShopFeatureSettings",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                table: "OutboxMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFeeAmount",
                table: "Orders",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DailyPointsBudget",
                table: "LoyaltyTenantConfigs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MonthlyPointsBudget",
                table: "LoyaltyTenantConfigs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PerCustomerDailyLimit",
                table: "LoyaltyTenantConfigs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PerOrderRateCap",
                table: "LoyaltyTenantConfigs",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PointsIssuedThisMonth",
                table: "LoyaltyTenantConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PointsIssuedToday",
                table: "LoyaltyTenantConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                table: "AccountingEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LoyaltyIssuanceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PointsIssued = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IssuedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    IsReversed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyIssuanceRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyIssuanceRecords_OrderId",
                table: "LoyaltyIssuanceRecords",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoyaltyIssuanceRecords");

            migrationBuilder.DropColumn(
                name: "PlatformFeeRate",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "PlatformFeeAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DailyPointsBudget",
                table: "LoyaltyTenantConfigs");

            migrationBuilder.DropColumn(
                name: "MonthlyPointsBudget",
                table: "LoyaltyTenantConfigs");

            migrationBuilder.DropColumn(
                name: "PerCustomerDailyLimit",
                table: "LoyaltyTenantConfigs");

            migrationBuilder.DropColumn(
                name: "PerOrderRateCap",
                table: "LoyaltyTenantConfigs");

            migrationBuilder.DropColumn(
                name: "PointsIssuedThisMonth",
                table: "LoyaltyTenantConfigs");

            migrationBuilder.DropColumn(
                name: "PointsIssuedToday",
                table: "LoyaltyTenantConfigs");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "AccountingEntries");
        }
    }
}

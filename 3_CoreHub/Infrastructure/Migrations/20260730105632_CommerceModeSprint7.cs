using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CommerceModeSprint7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Settings_CommerceModeOverride",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CommissionBase",
                table: "ProductReferralConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CommerceMode",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CommunityFundRate",
                table: "Orders",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostPrice",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryFee",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFeeRate",
                table: "Orders",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformMargin",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SellPrice",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CommunityFundSpendRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Recipient = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    SpentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    WalletTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityFundSpendRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductCostPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    CostPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCostPrices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromoCampaignRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PromoCampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoCampaignRecipients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromoCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    TotalRecipients = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SentCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    FailedCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SegmentSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoCampaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
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
                name: "IX_PromoCampaignRecipients_CustomerId",
                table: "PromoCampaignRecipients",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCampaignRecipients_PromoCampaignId_Status",
                table: "PromoCampaignRecipients",
                columns: new[] { "PromoCampaignId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PromoCampaigns_CreatedAt",
                table: "PromoCampaigns",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCampaigns_TenantId_Status",
                table: "PromoCampaigns",
                columns: new[] { "TenantId", "Status" });

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
                name: "PromoCampaignRecipients");

            migrationBuilder.DropTable(
                name: "PromoCampaigns");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropIndex(
                name: "IX_Orders_CommerceMode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Settings_CommerceModeOverride",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CommissionBase",
                table: "ProductReferralConfigs");

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

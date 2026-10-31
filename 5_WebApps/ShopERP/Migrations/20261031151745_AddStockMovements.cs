using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.ShopERP.Migrations
{
    /// <inheritdoc />
    public partial class AddStockMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountCharts");

            migrationBuilder.DropTable(
                name: "PeriodClosingStatuses");

            migrationBuilder.AddColumn<string>(
                name: "MappingVersion",
                table: "JournalEntry",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostingPurpose",
                table: "JournalEntry",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    IngredientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MovementType = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    PurchaseReceiptId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PurchaseReceiptLineId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MovementDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntry_TenantId_ReferenceType_ReferenceId_PostingPurpose",
                table: "JournalEntry",
                columns: new[] { "TenantId", "ReferenceType", "ReferenceId", "PostingPurpose" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_TenantId_IngredientId_MovementDate",
                table: "StockMovements",
                columns: new[] { "TenantId", "IngredientId", "MovementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_TenantId_PurchaseReceiptId",
                table: "StockMovements",
                columns: new[] { "TenantId", "PurchaseReceiptId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntry_TenantId_ReferenceType_ReferenceId_PostingPurpose",
                table: "JournalEntry");

            migrationBuilder.DropColumn(
                name: "MappingVersion",
                table: "JournalEntry");

            migrationBuilder.DropColumn(
                name: "PostingPurpose",
                table: "JournalEntry");

            migrationBuilder.CreateTable(
                name: "AccountCharts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AccountName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    IsNormalCredit = table.Column<bool>(type: "INTEGER", nullable: false),
                    Standard = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountCharts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PeriodClosingStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ClosedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    PeriodMonth = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodYear = table.Column<int>(type: "INTEGER", nullable: false),
                    ReopenReason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodClosingStatuses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountCharts_Standard",
                table: "AccountCharts",
                column: "Standard");

            migrationBuilder.CreateIndex(
                name: "IX_AccountCharts_Standard_AccountCode",
                table: "AccountCharts",
                columns: new[] { "Standard", "AccountCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PeriodClosingStatuses_TenantId",
                table: "PeriodClosingStatuses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodClosingStatuses_TenantId_PeriodYear_PeriodMonth",
                table: "PeriodClosingStatuses",
                columns: new[] { "TenantId", "PeriodYear", "PeriodMonth" },
                unique: true);
        }
    }
}

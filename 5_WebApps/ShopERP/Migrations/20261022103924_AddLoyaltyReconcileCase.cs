using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.ShopERP.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyReconcileCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoyaltyReconcileCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VoucherCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CustomerDeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Points = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResolvedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ResolutionNote = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyReconcileCases", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyReconcileCases_Status_FailedAt",
                table: "LoyaltyReconcileCases",
                columns: new[] { "Status", "FailedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoyaltyReconcileCases");
        }
    }
}

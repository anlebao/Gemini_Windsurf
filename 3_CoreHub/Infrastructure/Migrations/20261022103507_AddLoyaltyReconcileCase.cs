using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CustomerDeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FailedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ResolvedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
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

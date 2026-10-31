using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPaymentsC2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SourceEventId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IsSynthetic = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderPayments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderPayments_TenantId_OrderId",
                table: "OrderPayments",
                columns: new[] { "TenantId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderPayments_TenantId_SourceEventId",
                table: "OrderPayments",
                columns: new[] { "TenantId", "SourceEventId" },
                unique: true);

            // W4/C2 (task card §2.4): backfill 1 synthetic payment row per order đã Paid —
            // Amount = TotalAmount − DepositPaidAmount (tiền thu thực lúc confirm), Method="legacy",
            // SourceEventId="legacy-{orderId}" (unique → idempotent khi re-run).
            migrationBuilder.Sql("""
                INSERT INTO "OrderPayments"
                    ("Id", "OrderId", "Amount", "Method", "PaidAt", "SourceEventId", "Reference",
                     "IsSynthetic", "TenantId", "CreatedAt", "UpdatedAt", "CreatedBy", "UpdatedBy", "IsDeleted")
                SELECT gen_random_uuid(), o."Id",
                       o."TotalAmount" - COALESCE(o."DepositPaidAmount", 0),
                       'legacy',
                       COALESCE(o."UpdatedAt", o."CreatedAt", now()),
                       'legacy-' || o."Id"::text,
                       NULL, true, o."TenantId", now(), now(), NULL, NULL, false
                FROM "Orders" o
                WHERE o."PaymentStatus" = 'Paid'
                  AND NOT EXISTS (
                      SELECT 1 FROM "OrderPayments" p
                      WHERE p."OrderId" = o."Id" AND p."SourceEventId" = 'legacy-' || o."Id"::text);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderPayments");
        }
    }
}

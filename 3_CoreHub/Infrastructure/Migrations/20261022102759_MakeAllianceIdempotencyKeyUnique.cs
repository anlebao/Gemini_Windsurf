using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeAllianceIdempotencyKeyUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AllianceTransactions_IdempotencyKey",
                table: "AllianceTransactions");

            // P0.1 (R1): dedup existing rows before the unique index — if duplicate
            // non-null keys already exist (concurrent writes slipped past the old
            // application-layer check), keep the EARLIEST transaction's key and clear
            // the rest. Clearing (not deleting) preserves the append-only ledger.
            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT "Id",
                           ROW_NUMBER() OVER (PARTITION BY "IdempotencyKey" ORDER BY "TransactionAt", "Id") AS rn
                    FROM "AllianceTransactions"
                    WHERE "IdempotencyKey" IS NOT NULL
                )
                UPDATE "AllianceTransactions" t
                SET "IdempotencyKey" = NULL
                FROM ranked r
                WHERE t."Id" = r."Id" AND r.rn > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AllianceTransactions_IdempotencyKey",
                table: "AllianceTransactions",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AllianceTransactions_IdempotencyKey",
                table: "AllianceTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_AllianceTransactions_IdempotencyKey",
                table: "AllianceTransactions",
                column: "IdempotencyKey");
        }
    }
}

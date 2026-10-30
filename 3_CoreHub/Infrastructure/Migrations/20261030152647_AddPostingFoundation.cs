using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPostingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PostingPurpose",
                table: "JournalEntries",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostingPurpose",
                table: "AccountingEntries",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceEventId",
                table: "AccountingEntries",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceEventType",
                table: "AccountingEntries",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EntryPostingStatuses",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryPostingStatuses", x => x.EntryId);
                });

            // ===== SRS v1.2 Phase A — backfill TRƯỚC unique index (R1) =====

            // 1. JournalEntries.PostingPurpose: reversal refs → 'reversal', opening → 'opening-balance';
            //    Order JEs derive từ lines (1 order = 1 JE revenue + 1 JE cogs cùng ReferenceId —
            //    production evidence 2026-10-30: 14 refs × 2 JEs, mixed 5xx+6xx = 0).
            migrationBuilder.Sql("""
                UPDATE "JournalEntries" SET "PostingPurpose" = 'reversal'
                WHERE "PostingPurpose" IS NULL AND "ReferenceType" IN ('ManualReversal','OpeningBalanceReversal');
                UPDATE "JournalEntries" SET "PostingPurpose" = 'opening-balance'
                WHERE "PostingPurpose" IS NULL AND "ReferenceType" = 'OpeningBalance';
                UPDATE "JournalEntries" j SET "PostingPurpose" = 'cogs'
                WHERE j."PostingPurpose" IS NULL AND j."ReferenceType" = 'Order'
                  AND EXISTS (SELECT 1 FROM "JournalEntryLine" l WHERE l."JournalEntryId" = j."Id" AND l."AccountNumber" ~ '^6');
                UPDATE "JournalEntries" j SET "PostingPurpose" = 'deposit-offset'
                WHERE j."PostingPurpose" IS NULL AND j."ReferenceType" = 'Order'
                  AND EXISTS (SELECT 1 FROM "JournalEntryLine" l WHERE l."JournalEntryId" = j."Id" AND l."AccountNumber" = '3387');
                UPDATE "JournalEntries" j SET "PostingPurpose" = 'revenue'
                WHERE j."PostingPurpose" IS NULL AND j."ReferenceType" = 'Order';
                UPDATE "JournalEntries" SET "PostingPurpose" = 'original'
                WHERE "PostingPurpose" IS NULL;
                """);

            // 2. AccountingEntries.PostingPurpose (informational discriminator — đặc thù trước, chung sau)
            migrationBuilder.Sql("""
                UPDATE "AccountingEntries" SET "PostingPurpose" = 'reversal' WHERE "ReversalEntryId" IS NOT NULL AND "PostingPurpose" IS NULL;
                UPDATE "AccountingEntries" SET "PostingPurpose" = 'deposit-offset' WHERE "EntryType" = 5 AND "PostingPurpose" IS NULL;
                UPDATE "AccountingEntries" SET "PostingPurpose" = 'vat' WHERE "AccountCode" = '3331' AND "Reference" IS NOT NULL AND "PostingPurpose" IS NULL;
                UPDATE "AccountingEntries" SET "PostingPurpose" = 'cogs' WHERE "AccountCode" IN ('632','611','612') AND "Reference" IS NOT NULL AND "PostingPurpose" IS NULL;
                UPDATE "AccountingEntries" SET "PostingPurpose" = 'revenue' WHERE "AccountCode" IN ('511','512') AND "Reference" IS NOT NULL AND "PostingPurpose" IS NULL;
                UPDATE "AccountingEntries" SET "PostingPurpose" = 'opening-balance' WHERE "Description" LIKE 'Số dư đầu kỳ%' AND "PostingPurpose" IS NULL;
                UPDATE "AccountingEntries" SET "PostingPurpose" = 'receivable' WHERE "AccountCode" = '131' AND "Amount" > 0 AND "PostingPurpose" IS NULL;
                UPDATE "AccountingEntries" SET "PostingPurpose" = 'receivable-payment' WHERE "AccountCode" = '131' AND "Amount" < 0 AND "PostingPurpose" IS NULL;
                UPDATE "AccountingEntries" SET "PostingPurpose" = 'payable' WHERE "AccountCode" = '331' AND "Amount" > 0 AND "PostingPurpose" IS NULL;
                UPDATE "AccountingEntries" SET "PostingPurpose" = 'payable-payment' WHERE "AccountCode" = '331' AND "Amount" < 0 AND "PostingPurpose" IS NULL;
                UPDATE "AccountingEntries" SET "PostingPurpose" = 'manual-receipt' WHERE "EntryType" = 1 AND "PostingPurpose" IS NULL;
                UPDATE "AccountingEntries" SET "PostingPurpose" = 'manual-expense' WHERE "EntryType" = 2 AND "PostingPurpose" IS NULL;
                UPDATE "AccountingEntries" SET "PostingPurpose" = 'other' WHERE "PostingPurpose" IS NULL;
                """);

            // 3. Seed EntryPostingStatuses: Posted nếu đã có JE — qua ManualEntry-style (ReferenceId=entry.Id)
            //    HOẶC Order JE (ReferenceId=order guid prefix-matching entry.Reference — order ref có
            //    thể mang suffix '-DEPOSIT-OFFSET'...). Skipped cho Adjustment/reversal, Pending còn lại.
            migrationBuilder.Sql("""
                INSERT INTO "EntryPostingStatuses" ("EntryId","TenantId","Status","Reason","Attempts","CreatedAt","UpdatedAt")
                SELECT e."Id", e."TenantId"::uuid,
                    CASE
                        WHEN EXISTS (SELECT 1 FROM "JournalEntries" j WHERE j."ReferenceId" = e."Id"
                                     AND j."ReferenceType" IN ('ManualEntry','ManualReversal','OpeningBalance','OpeningBalanceReversal'))
                            THEN 1
                        WHEN e."Reference" IS NOT NULL AND EXISTS (
                            SELECT 1 FROM "JournalEntries" j WHERE j."ReferenceType" = 'Order'
                            AND e."Reference" LIKE (j."ReferenceId"::text || '%'))
                            THEN 1
                        WHEN e."EntryType" = 4 OR e."ReversalEntryId" IS NOT NULL THEN 2
                        ELSE 0
                    END,
                    'backfill', 0, NOW(), NOW()
                FROM "AccountingEntries" e;
                """);

            // 4. Pre-scan dup: ABORT rõ ràng nếu có cặp trùng (sẽ vi phạm unique index)
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "JournalEntries"
                        WHERE "ReferenceId" IS NOT NULL
                        GROUP BY "TenantId","ReferenceType","ReferenceId","PostingPurpose"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'AddPostingFoundation: duplicate JournalEntries for (TenantId,ReferenceType,ReferenceId,PostingPurpose) — dedup trước khi tạo unique index';
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_TenantId_ReferenceType_ReferenceId_PostingPu~",
                table: "JournalEntries",
                columns: new[] { "TenantId", "ReferenceType", "ReferenceId", "PostingPurpose" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntryPostingStatuses_Status_UpdatedAt",
                table: "EntryPostingStatuses",
                columns: new[] { "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EntryPostingStatuses_TenantId",
                table: "EntryPostingStatuses",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntryPostingStatuses");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_TenantId_ReferenceType_ReferenceId_PostingPu~",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "PostingPurpose",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "PostingPurpose",
                table: "AccountingEntries");

            migrationBuilder.DropColumn(
                name: "SourceEventId",
                table: "AccountingEntries");

            migrationBuilder.DropColumn(
                name: "SourceEventType",
                table: "AccountingEntries");
        }
    }
}

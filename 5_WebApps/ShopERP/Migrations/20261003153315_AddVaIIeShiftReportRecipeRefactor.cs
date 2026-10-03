using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.ShopERP.Migrations
{
    /// <inheritdoc />
    public partial class AddVaIIeShiftReportRecipeRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ═══════════════════════════════════════════════════════════════════
            // VA-IIE Sprint B — Recipe refactor (flat → header + RecipeLine) + backfill.
            // Approved 2026-10-03: Q1-A (backfill TOÀN BỘ — mỗi row flat cũ → Recipe v1 + RecipeLine,
            // IsActive theo product) + Q3-C (đơn vị cơ sở khối lượng kg → g, ×1000).
            // ORDER MATTERS: mọi data transformation chạy TRƯỚC các DropColumn/table-rebuild.
            // ═══════════════════════════════════════════════════════════════════

            // ── 1. Recipe header columns (additive — mọi row flat cũ trở thành header) ──
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "WasteFactor",
                table: "Recipes",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Yield",
                table: "Recipes",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveFrom",
                table: "Recipes",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // ── 2. Ingredient extend (additive) ──
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Ingredients",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "VarianceThresholdPercent",
                table: "Ingredients",
                type: "TEXT",
                precision: 5,
                scale: 2,
                nullable: true);

            // ── 3. Header defaults từ dữ liệu cũ: IsActive theo product; EffectiveFrom = CreatedAt ──
            migrationBuilder.Sql("""
                UPDATE "Recipes"
                SET "IsActive" = COALESCE((SELECT p."IsActive" FROM "Products" p WHERE p."Id" = "Recipes"."ProductId"), 1),
                    "EffectiveFrom" = "CreatedAt";
                """);

            // ── 4. Staging: MỌI row flat cũ → 1 RecipeLine (referencing keeper header Id per product).
            //        kg → g conversion (Q3-C): quantity ×1000, unit 'kg' → 'g'. Phi-SI units giữ nguyên. ──
            migrationBuilder.Sql("""
                CREATE TABLE "RecipeLines_Backfill" (
                    "RecipeId" TEXT NOT NULL,
                    "IngredientId" TEXT NOT NULL,
                    "Quantity" TEXT NOT NULL,
                    "Unit" TEXT NOT NULL,
                    "TenantId" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL
                );
                """);

            migrationBuilder.Sql("""
                INSERT INTO "RecipeLines_Backfill" ("RecipeId", "IngredientId", "Quantity", "Unit", "TenantId", "CreatedAt", "UpdatedAt")
                SELECT k."Id", r."IngredientId",
                       CASE WHEN i."Unit" = 'kg' THEN r."QuantityNeeded" * 1000 ELSE r."QuantityNeeded" END,
                       CASE WHEN i."Unit" = 'kg' THEN 'g' ELSE COALESCE(i."Unit", 'cái') END,
                       r."TenantId", r."CreatedAt", r."UpdatedAt"
                FROM "Recipes" r
                JOIN "Ingredients" i ON i."Id" = r."IngredientId"
                JOIN "Recipes" k ON k."rowid" = (
                    SELECT MIN(k2."rowid") FROM "Recipes" k2
                    WHERE k2."TenantId" = r."TenantId" AND k2."ProductId" = r."ProductId"
                );
                """);

            // ── 5. Headers: giữ 1 row per (TenantId, ProductId) — keeper = MIN(rowid); xoá các row dư ──
            migrationBuilder.Sql("""
                DELETE FROM "Recipes" WHERE "rowid" NOT IN (
                    SELECT MIN("rowid") FROM "Recipes" GROUP BY "TenantId", "ProductId"
                );
                """);

            // ── 6. Ingredient base unit kg → g (Q3-C): stock/threshold ×1000, price ÷1000 ──
            migrationBuilder.Sql("""
                UPDATE "Ingredients"
                SET "Unit" = 'g',
                    "CurrentStock" = "CurrentStock" * 1000,
                    "MinStockThreshold" = "MinStockThreshold" * 1000,
                    "PricePerUnit" = "PricePerUnit" / 1000
                WHERE "Unit" = 'kg';
                """);

            // ── 7. Bỏ 2 cột flat (SQLite table rebuild — data đã an toàn trong staging) ──
            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_Ingredients_IngredientId",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_IngredientId",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "IngredientId",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "QuantityNeeded",
                table: "Recipes");

            migrationBuilder.CreateTable(
                name: "RecipeLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IngredientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeLines_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeLines_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ── 8. Backfill RecipeLines từ staging (headers đã sẵn sàng) + xoá staging ──
            migrationBuilder.Sql("""
                INSERT INTO "RecipeLines" ("Id", "TenantId", "RecipeId", "IngredientId", "Quantity", "Unit", "CreatedAt", "UpdatedAt", "IsDeleted", "CreatedBy", "UpdatedBy")
                SELECT lower(hex(randomblob(16))), "TenantId", "RecipeId", "IngredientId", "Quantity", "Unit", "CreatedAt", "UpdatedAt", 0, NULL, NULL
                FROM "RecipeLines_Backfill";
                """);

            migrationBuilder.Sql("""
                DROP TABLE "RecipeLines_Backfill";
                """);

            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShiftType = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    StaffUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AcknowledgedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    HandoverNotes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CashCount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    PosCashTotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shifts_Users_StaffUserId",
                        column: x => x.StaffUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryCounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShiftId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IngredientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CountType = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    MidShiftStockIn = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryCounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryCounts_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCounts_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShiftAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShiftId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AlertCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IngredientId = table.Column<Guid>(type: "TEXT", nullable: true),
                    VarianceValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    VariancePercent = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: true),
                    IsResolved = table.Column<bool>(type: "INTEGER", nullable: false),
                    ResolvedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
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
                    table.PrimaryKey("PK_ShiftAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftAlerts_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftAlerts_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TheoreticalConsumptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShiftId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IngredientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TheoreticalQuantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    ActualQuantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Variance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    VariancePercent = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TheoreticalConsumptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TheoreticalConsumptions_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TheoreticalConsumptions_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_TenantId_IsActive",
                table: "Recipes",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_TenantId_Category",
                table: "Ingredients",
                columns: new[] { "TenantId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCounts_IngredientId",
                table: "InventoryCounts",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCounts_ShiftId",
                table: "InventoryCounts",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCounts_TenantId_IngredientId",
                table: "InventoryCounts",
                columns: new[] { "TenantId", "IngredientId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCounts_TenantId_ShiftId",
                table: "InventoryCounts",
                columns: new[] { "TenantId", "ShiftId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeLines_IngredientId",
                table: "RecipeLines",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeLines_RecipeId",
                table: "RecipeLines",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeLines_TenantId_IngredientId",
                table: "RecipeLines",
                columns: new[] { "TenantId", "IngredientId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeLines_TenantId_RecipeId",
                table: "RecipeLines",
                columns: new[] { "TenantId", "RecipeId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAlerts_IngredientId",
                table: "ShiftAlerts",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAlerts_ShiftId",
                table: "ShiftAlerts",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAlerts_TenantId_IsResolved",
                table: "ShiftAlerts",
                columns: new[] { "TenantId", "IsResolved" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAlerts_TenantId_ShiftId",
                table: "ShiftAlerts",
                columns: new[] { "TenantId", "ShiftId" });

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_StaffUserId",
                table: "Shifts",
                column: "StaffUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_TenantId_StaffUserId",
                table: "Shifts",
                columns: new[] { "TenantId", "StaffUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_TenantId_Status",
                table: "Shifts",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TheoreticalConsumptions_IngredientId",
                table: "TheoreticalConsumptions",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_TheoreticalConsumptions_ShiftId",
                table: "TheoreticalConsumptions",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_TheoreticalConsumptions_TenantId_IngredientId",
                table: "TheoreticalConsumptions",
                columns: new[] { "TenantId", "IngredientId" });

            migrationBuilder.CreateIndex(
                name: "IX_TheoreticalConsumptions_TenantId_ShiftId",
                table: "TheoreticalConsumptions",
                columns: new[] { "TenantId", "ShiftId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── Reverse backfill: Recipe header + RecipeLine → flat rows (best-effort, giữ traceability) ──
            migrationBuilder.Sql("""
                CREATE TABLE "Recipes_FlatBackfill" (
                    "Id" TEXT NOT NULL,
                    "TenantId" TEXT NOT NULL,
                    "ProductId" TEXT NOT NULL,
                    "IngredientId" TEXT NOT NULL,
                    "QuantityNeeded" TEXT NOT NULL,
                    "Unit" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    "CreatedBy" TEXT NULL,
                    "UpdatedBy" TEXT NULL,
                    "IsDeleted" INTEGER NOT NULL
                );
                """);

            migrationBuilder.Sql("""
                INSERT INTO "Recipes_FlatBackfill" ("Id", "TenantId", "ProductId", "IngredientId", "QuantityNeeded", "Unit", "CreatedAt", "UpdatedAt", "CreatedBy", "UpdatedBy", "IsDeleted")
                SELECT r."Id", r."TenantId", r."ProductId", l."IngredientId", l."Quantity", l."Unit",
                       r."CreatedAt", r."UpdatedAt", r."CreatedBy", r."UpdatedBy", r."IsDeleted"
                FROM "Recipes" r
                JOIN "RecipeLines" l ON l."RecipeId" = r."Id";
                """);

            migrationBuilder.DropTable(
                name: "InventoryCounts");

            migrationBuilder.DropTable(
                name: "RecipeLines");

            migrationBuilder.DropTable(
                name: "ShiftAlerts");

            migrationBuilder.DropTable(
                name: "TheoreticalConsumptions");

            migrationBuilder.DropTable(
                name: "Shifts");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_TenantId_IsActive",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_Ingredients_TenantId_Category",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "WasteFactor",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Yield",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "EffectiveFrom",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "VarianceThresholdPercent",
                table: "Ingredients");

            migrationBuilder.AddColumn<Guid>(
                name: "IngredientId",
                table: "Recipes",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "QuantityNeeded",
                table: "Recipes",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            // ── Khôi phục flat rows + reverse đơn vị g → kg (đối xứng với Up) ──
            migrationBuilder.Sql("""
                INSERT INTO "Recipes" ("Id", "TenantId", "ProductId", "IngredientId", "QuantityNeeded", "CreatedAt", "UpdatedAt", "CreatedBy", "UpdatedBy", "IsDeleted")
                SELECT "Id", "TenantId", "ProductId", "IngredientId",
                       CASE WHEN "Unit" = 'g' THEN "QuantityNeeded" / 1000 ELSE "QuantityNeeded" END,
                       "CreatedAt", "UpdatedAt", "CreatedBy", "UpdatedBy", "IsDeleted"
                FROM "Recipes_FlatBackfill";
                """);

            migrationBuilder.Sql("""
                UPDATE "Ingredients"
                SET "Unit" = 'kg',
                    "CurrentStock" = "CurrentStock" / 1000,
                    "MinStockThreshold" = "MinStockThreshold" / 1000,
                    "PricePerUnit" = "PricePerUnit" * 1000
                WHERE "Unit" = 'g';
                """);

            migrationBuilder.Sql("""
                DROP TABLE "Recipes_FlatBackfill";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_IngredientId",
                table: "Recipes",
                column: "IngredientId");

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_Ingredients_IngredientId",
                table: "Recipes",
                column: "IngredientId",
                principalTable: "Ingredients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

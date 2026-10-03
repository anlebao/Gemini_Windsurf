using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVaIIeShiftReportRecipeRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ═══════════════════════════════════════════════════════════════════
            // VA-IIE Sprint B — Recipe refactor (flat → header + RecipeLine) + backfill (PG).
            // Approved 2026-10-03: Q1-A (backfill TOÀN BỘ — IsActive theo product) + Q3-C (kg → g).
            // PG Recipes thường rỗng (seed chạy per-tenant SQLite) — SQL an toàn cho cả 2 case.
            // ═══════════════════════════════════════════════════════════════════

            // ── 1. Recipe header columns (additive — mọi row flat cũ trở thành header) ──
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Recipes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Recipes",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "WasteFactor",
                table: "Recipes",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Yield",
                table: "Recipes",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveFrom",
                table: "Recipes",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            // ── 2. Ingredient extend (additive) ──
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Ingredients",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "VarianceThresholdPercent",
                table: "Ingredients",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            // ── 3. Header defaults từ dữ liệu cũ: IsActive theo product; EffectiveFrom = CreatedAt ──
            migrationBuilder.Sql("""
                UPDATE "Recipes"
                SET "IsActive" = COALESCE((SELECT p."IsActive" FROM "Products" p WHERE p."Id" = "Recipes"."ProductId"), true),
                    "EffectiveFrom" = "CreatedAt";
                """);

            migrationBuilder.CreateTable(
                name: "RecipeLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
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

            // ── 4. Backfill: MỌI row flat cũ → RecipeLine (keeper header = MIN(Id) per product); kg → g ──
            migrationBuilder.Sql("""
                INSERT INTO "RecipeLines" ("Id", "TenantId", "RecipeId", "IngredientId", "Quantity", "Unit", "CreatedAt", "UpdatedAt", "IsDeleted", "CreatedBy", "UpdatedBy")
                SELECT gen_random_uuid(), r."TenantId", k."KeeperId", r."IngredientId",
                       CASE WHEN i."Unit" = 'kg' THEN r."QuantityNeeded" * 1000 ELSE r."QuantityNeeded" END,
                       CASE WHEN i."Unit" = 'kg' THEN 'g' ELSE COALESCE(i."Unit", 'cái') END,
                       r."CreatedAt", r."UpdatedAt", false, NULL, NULL
                FROM "Recipes" r
                JOIN "Ingredients" i ON i."Id" = r."IngredientId"
                JOIN (
                    -- Keeper header per (TenantId, ProductId) — ROW_NUMBER (PG không có min(uuid))
                    SELECT "TenantId", "ProductId", "Id" AS "KeeperId",
                           ROW_NUMBER() OVER (PARTITION BY "TenantId", "ProductId" ORDER BY "Id") AS rn
                    FROM "Recipes"
                ) k ON k."TenantId" = r."TenantId" AND k."ProductId" = r."ProductId" AND k.rn = 1;
                """);

            // ── 5. Headers: giữ 1 row per (TenantId, ProductId) — keeper = row đầu (ORDER BY Id); xoá các row dư ──
            migrationBuilder.Sql("""
                DELETE FROM "Recipes" r USING (
                    SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "TenantId", "ProductId" ORDER BY "Id") AS rn
                    FROM "Recipes"
                ) x WHERE x."Id" = r."Id" AND x.rn > 1;
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

            // ── 7. Bỏ 2 cột flat (FK đã drop ở bước này — PG ALTER native) ──
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
                name: "Shifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftType = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    StaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcknowledgedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    HandoverNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CashCount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PosCashTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountType = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MidShiftStockIn = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: true),
                    VarianceValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    VariancePercent = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    TheoreticalQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ActualQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Variance = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    VariancePercent = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
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
                    "Id" uuid NOT NULL,
                    "TenantId" uuid NOT NULL,
                    "ProductId" uuid NOT NULL,
                    "IngredientId" uuid NOT NULL,
                    "QuantityNeeded" numeric(18,4) NOT NULL,
                    "Unit" text NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL,
                    "UpdatedAt" timestamp without time zone NOT NULL,
                    "CreatedBy" text NULL,
                    "UpdatedBy" text NULL,
                    "IsDeleted" boolean NOT NULL
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
                name: "EffectiveFrom",
                table: "Recipes");

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
                name: "Category",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "VarianceThresholdPercent",
                table: "Ingredients");

            migrationBuilder.AddColumn<Guid>(
                name: "IngredientId",
                table: "Recipes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "QuantityNeeded",
                table: "Recipes",
                type: "numeric(18,4)",
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

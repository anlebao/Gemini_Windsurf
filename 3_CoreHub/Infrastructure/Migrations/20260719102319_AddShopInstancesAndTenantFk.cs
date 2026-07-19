using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShopInstancesAndTenantFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ShopInstanceId",
                table: "Tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ShopInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaxTenants = table.Column<int>(type: "integer", nullable: false, defaultValue: 50),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    HealthCheckUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LastHealthCheck = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    HealthStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Unknown"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopInstances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_ShopInstanceId",
                table: "Tenants",
                column: "ShopInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopInstances_BaseUrl",
                table: "ShopInstances",
                column: "BaseUrl",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_ShopInstances_ShopInstanceId",
                table: "Tenants",
                column: "ShopInstanceId",
                principalTable: "ShopInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Phase 1: Seed default ShopInstance + backfill all existing tenants.
            // Deterministic Guid for local dev — production can update BaseUrl via Admin UI (Phase 6).
            migrationBuilder.Sql(@"
                INSERT INTO ""ShopInstances"" (""Id"", ""BaseUrl"", ""Label"", ""MaxTenants"", ""IsActive"", ""HealthStatus"", ""TenantId"", ""CreatedAt"", ""UpdatedAt"")
                VALUES (
                    '00000000-0000-0000-0000-000000000001',
                    'http://shoperp:5003',
                    'Default Local',
                    50,
                    true,
                    'Unknown',
                    '00000000-0000-0000-0000-000000000000',
                    CURRENT_TIMESTAMP,
                    CURRENT_TIMESTAMP
                )
                ON CONFLICT (""BaseUrl"") DO NOTHING;

                UPDATE ""Tenants""
                SET ""ShopInstanceId"" = '00000000-0000-0000-0000-000000000001'
                WHERE ""ShopInstanceId"" IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_ShopInstances_ShopInstanceId",
                table: "Tenants");

            migrationBuilder.DropTable(
                name: "ShopInstances");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_ShopInstanceId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ShopInstanceId",
                table: "Tenants");
        }
    }
}

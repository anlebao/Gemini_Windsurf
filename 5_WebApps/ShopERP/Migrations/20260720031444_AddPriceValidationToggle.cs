using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.ShopERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceValidationToggle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Products_ProductId",
                table: "OrderItems");

            migrationBuilder.AddColumn<Guid>(
                name: "ShopInstanceId",
                table: "Tenants",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Price_Validation_Enabled",
                table: "ShopFeatureSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RoutingKey",
                table: "OutboxMessages",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ShopInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    MaxTenants = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 50),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    HealthCheckUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    LastHealthCheck = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HealthStatus = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Unknown"),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
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
                name: "FK_OrderItems_Products_ProductId",
                table: "OrderItems",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_ShopInstances_ShopInstanceId",
                table: "Tenants",
                column: "ShopInstanceId",
                principalTable: "ShopInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Products_ProductId",
                table: "OrderItems");

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

            migrationBuilder.DropColumn(
                name: "Price_Validation_Enabled",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "RoutingKey",
                table: "OutboxMessages");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Products_ProductId",
                table: "OrderItems",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

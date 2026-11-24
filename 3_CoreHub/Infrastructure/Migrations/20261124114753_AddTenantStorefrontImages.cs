using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantStorefrontImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Settings_StorefrontBackdropUrl",
                table: "Tenants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Settings_StorefrontBannerUrl",
                table: "Tenants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerContextDismissalEntity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DismissedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerContextDismissalEntity", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContextDismissalEntity_CustomerId_ExpiresAt",
                table: "CustomerContextDismissalEntity",
                columns: new[] { "CustomerId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContextDismissalEntity_TenantId_CustomerId_CardId",
                table: "CustomerContextDismissalEntity",
                columns: new[] { "TenantId", "CustomerId", "CardId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerContextDismissalEntity");

            migrationBuilder.DropColumn(
                name: "Settings_StorefrontBackdropUrl",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Settings_StorefrontBannerUrl",
                table: "Tenants");
        }
    }
}

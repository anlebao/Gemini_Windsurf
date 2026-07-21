using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantProfileSlugAndSectionToggles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Settings_Slug",
                table: "Tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AIChat_Enabled",
                table: "ShopFeatureSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Campaign_Section_Enabled",
                table: "ShopFeatureSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "GoogleMap_Section_Enabled",
                table: "ShopFeatureSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SocialHub_Section_Enabled",
                table: "ShopFeatureSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "VibeShowcase_Section_Enabled",
                table: "ShopFeatureSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Settings_Slug",
                table: "Tenants",
                column: "Settings_Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_Settings_Slug",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Settings_Slug",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "AIChat_Enabled",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Campaign_Section_Enabled",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "GoogleMap_Section_Enabled",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "SocialHub_Section_Enabled",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "VibeShowcase_Section_Enabled",
                table: "ShopFeatureSettings");
        }
    }
}

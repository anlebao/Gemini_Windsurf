using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeSectionToggles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Home_CampaignSection_Enabled",
                table: "ShopFeatureSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Home_FeaturedSection_Enabled",
                table: "ShopFeatureSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Home_SocialHub_Enabled",
                table: "ShopFeatureSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Home_StoreSection_Enabled",
                table: "ShopFeatureSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Home_CampaignSection_Enabled",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Home_FeaturedSection_Enabled",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Home_SocialHub_Enabled",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Home_StoreSection_Enabled",
                table: "ShopFeatureSettings");
        }
    }
}

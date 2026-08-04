using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.ShopERP.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSettingsB09DNAndStyleColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Settings_BusinessField",
                table: "Tenants",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Settings_CharterCapital",
                table: "Tenants",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Settings_FooterColor",
                table: "Tenants",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Settings_HeaderColor",
                table: "Tenants",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Settings_LegalForm",
                table: "Tenants",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Settings_NavColor",
                table: "Tenants",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Home_CampaignSection_Enabled",
                table: "ShopFeatureSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Home_FeaturedSection_Enabled",
                table: "ShopFeatureSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Home_SocialHub_Enabled",
                table: "ShopFeatureSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Home_StoreSection_Enabled",
                table: "ShopFeatureSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Settings_BusinessField",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Settings_CharterCapital",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Settings_FooterColor",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Settings_HeaderColor",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Settings_LegalForm",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Settings_NavColor",
                table: "Tenants");

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

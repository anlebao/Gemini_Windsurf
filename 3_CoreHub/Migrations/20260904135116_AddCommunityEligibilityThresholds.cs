using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityEligibilityThresholds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Community_RequiredIdentityLevel",
                table: "ShopFeatureSettings",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "Community_SalesmanMinPoints",
                table: "ShopFeatureSettings",
                type: "integer",
                nullable: false,
                defaultValue: 1000);

            migrationBuilder.AddColumn<int>(
                name: "Community_ShipperMinPoints",
                table: "ShopFeatureSettings",
                type: "integer",
                nullable: false,
                defaultValue: 1000);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Community_RequiredIdentityLevel",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Community_SalesmanMinPoints",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Community_ShipperMinPoints",
                table: "ShopFeatureSettings");
        }
    }
}

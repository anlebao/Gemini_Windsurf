using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.ShopERP.Migrations
{
    /// <inheritdoc />
    public partial class AddRedemptionCatalogIsGlobal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Loyalty_RequirePhoneVerificationForRedeem",
                table: "ShopFeatureSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsGlobal",
                table: "RedemptionCatalogItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Loyalty_RequirePhoneVerificationForRedeem",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "IsGlobal",
                table: "RedemptionCatalogItems");
        }
    }
}

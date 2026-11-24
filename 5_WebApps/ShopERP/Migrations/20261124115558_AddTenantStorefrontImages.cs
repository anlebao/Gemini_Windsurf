using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.ShopERP.Migrations
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
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Settings_StorefrontBannerUrl",
                table: "Tenants",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Settings_StorefrontBackdropUrl",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Settings_StorefrontBannerUrl",
                table: "Tenants");
        }
    }
}

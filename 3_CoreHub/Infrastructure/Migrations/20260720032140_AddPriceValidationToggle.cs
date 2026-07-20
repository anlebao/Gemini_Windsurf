using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceValidationToggle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: FK_OrderItems_Products_ProductId was already dropped by AddOutboxRoutingKey (Phase 3).
            // Do NOT drop/re-add it here — that causes "constraint does not exist" on VPS where Phase 3 already ran.
            migrationBuilder.AddColumn<bool>(
                name: "Price_Validation_Enabled",
                table: "ShopFeatureSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Price_Validation_Enabled",
                table: "ShopFeatureSettings");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.ShopERP.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyFormulaAndNotificationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Loyalty_AwardOnAllOrders",
                table: "ShopFeatureSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "Loyalty_MaxPointsPerOrder",
                table: "ShopFeatureSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Loyalty_MinPointsPerOrder",
                table: "ShopFeatureSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Loyalty_PointsRate",
                table: "ShopFeatureSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "Notify_BirthdayBonus",
                table: "ShopFeatureSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "Notify_MissionCompleted",
                table: "ShopFeatureSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "Notify_RedemptionCancelled",
                table: "ShopFeatureSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "Notify_RedemptionFulfilled",
                table: "ShopFeatureSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "Notify_VoucherExpiringSoon",
                table: "ShopFeatureSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "VoucherExpiryNotifyHours",
                table: "ShopFeatureSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 24);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Loyalty_AwardOnAllOrders",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Loyalty_MaxPointsPerOrder",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Loyalty_MinPointsPerOrder",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Loyalty_PointsRate",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Notify_BirthdayBonus",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Notify_MissionCompleted",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Notify_RedemptionCancelled",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Notify_RedemptionFulfilled",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Notify_VoucherExpiringSoon",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "VoucherExpiryNotifyHours",
                table: "ShopFeatureSettings");
        }
    }
}

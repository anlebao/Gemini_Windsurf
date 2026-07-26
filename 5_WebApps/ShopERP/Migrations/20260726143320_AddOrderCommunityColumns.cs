using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.ShopERP.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderCommunityColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CodAmount",
                table: "Orders",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CodCollectedAt",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DeliveryLat",
                table: "Orders",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DeliveryLng",
                table: "Orders",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferralCode",
                table: "Orders",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferralProductId",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SalesmanId",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ShipperId",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ReferralProductId",
                table: "Orders",
                column: "ReferralProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_SalesmanId",
                table: "Orders",
                column: "SalesmanId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ShipperId",
                table: "Orders",
                column: "ShipperId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_ReferralProductId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_SalesmanId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_ShipperId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CodAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CodCollectedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryLat",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryLng",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReferralCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReferralProductId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SalesmanId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShipperId",
                table: "Orders");
        }
    }
}

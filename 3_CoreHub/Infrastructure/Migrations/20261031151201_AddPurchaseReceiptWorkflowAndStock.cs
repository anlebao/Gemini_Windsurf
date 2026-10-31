using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseReceiptWorkflowAndStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "PurchaseReceipts",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                table: "PurchaseReceipts",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "PurchaseReceipts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PostedAt",
                table: "PurchaseReceipts",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StockAppliedAt",
                table: "PurchaseReceipts",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IngredientId",
                table: "PurchaseReceiptLines",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "PurchaseReceipts");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "PurchaseReceipts");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "PurchaseReceipts");

            migrationBuilder.DropColumn(
                name: "PostedAt",
                table: "PurchaseReceipts");

            migrationBuilder.DropColumn(
                name: "StockAppliedAt",
                table: "PurchaseReceipts");

            migrationBuilder.DropColumn(
                name: "IngredientId",
                table: "PurchaseReceiptLines");
        }
    }
}

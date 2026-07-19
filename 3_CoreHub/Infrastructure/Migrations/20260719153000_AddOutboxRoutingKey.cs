using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxRoutingKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Phase 3 (Multi-VPS Checkout): Add RoutingKey column to OutboxMessages.
            // When set, NatsSyncWorker.BuildSubject appends ".{routingKey}" to the NATS subject
            // so only the correct ShopERP instance receives the event.
            // Nullable — existing events have no routing key (backward compatible).
            migrationBuilder.AddColumn<string>(
                name: "RoutingKey",
                table: "OutboxMessages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // Phase 3 (Option C): Drop FK_OrderItems_Products_ProductId — Gateway PG no longer stores Products.
            // OrderItem.ProductId is now a plain Guid column (snapshot from client at checkout time).
            // Products live in ShopERP SQLite. Referential integrity enforced at ShopERP level.
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Products_ProductId",
                table: "OrderItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-add FK (Option C revert — not recommended)
            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Products_ProductId",
                table: "OrderItems",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropColumn(
                name: "RoutingKey",
                table: "OutboxMessages");
        }
    }
}

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RoutingKey",
                table: "OutboxMessages");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingAttributionSessionUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AttributionSessions_TenantId_QrId_AnonymousSessionId",
                table: "AttributionSessions");

            migrationBuilder.AddColumn<Guid>(
                name: "WalletTransactionId",
                table: "CommissionLedgerEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttributionSessions_TenantId_QrId_AnonymousSessionId",
                table: "AttributionSessions",
                columns: new[] { "TenantId", "QrId", "AnonymousSessionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AttributionSessions_TenantId_QrId_AnonymousSessionId",
                table: "AttributionSessions");

            migrationBuilder.DropColumn(
                name: "WalletTransactionId",
                table: "CommissionLedgerEntries");

            migrationBuilder.CreateIndex(
                name: "IX_AttributionSessions_TenantId_QrId_AnonymousSessionId",
                table: "AttributionSessions",
                columns: new[] { "TenantId", "QrId", "AnonymousSessionId" });
        }
    }
}

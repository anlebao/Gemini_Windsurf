using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingIdempotencyAndConflictGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookingIdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingIdempotencyRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TenantId_StaffId_StartAt",
                table: "Bookings",
                columns: new[] { "TenantId", "StaffId", "StartAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingIdempotencyRecords_IdempotencyKey",
                table: "BookingIdempotencyRecords",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingIdempotencyRecords_TenantId_BookingId",
                table: "BookingIdempotencyRecords",
                columns: new[] { "TenantId", "BookingId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingIdempotencyRecords");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_TenantId_StaffId_StartAt",
                table: "Bookings");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingStaffSlotUniqueFiltered : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_TenantId_StaffId_StartAt",
                table: "Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TenantId_StaffId_StartAt",
                table: "Bookings",
                columns: new[] { "TenantId", "StaffId", "StartAt" },
                unique: true,
                filter: "\"Status\" IN (1, 2, 3, 4, 5)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_TenantId_StaffId_StartAt",
                table: "Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TenantId_StaffId_StartAt",
                table: "Bookings",
                columns: new[] { "TenantId", "StaffId", "StartAt" },
                unique: true);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOfferingNeedsReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NeedsReview",
                table: "AppointmentOfferings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NeedsReview",
                table: "AppointmentOfferings");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.Atrs.Infra.Migrations
{
    /// <inheritdoc />
    public partial class FixRowVersionXmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "row_version",
                table: "account_trust_profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "account_trust_profiles",
                type: "bytea",
                rowVersion: true,
                nullable: false);
        }
    }
}

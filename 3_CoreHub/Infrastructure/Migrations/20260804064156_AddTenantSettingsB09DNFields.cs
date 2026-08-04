using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSettingsB09DNFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Settings_BusinessField",
                table: "Tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Settings_CharterCapital",
                table: "Tenants",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Settings_LegalForm",
                table: "Tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Settings_BusinessField",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Settings_CharterCapital",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Settings_LegalForm",
                table: "Tenants");
        }
    }
}

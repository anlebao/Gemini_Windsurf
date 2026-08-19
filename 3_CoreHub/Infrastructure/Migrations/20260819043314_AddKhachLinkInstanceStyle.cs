using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKhachLinkInstanceStyle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FooterColor",
                table: "KhachLinkInstances",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeaderColor",
                table: "KhachLinkInstances",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "KhachLinkInstances",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NavColor",
                table: "KhachLinkInstances",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Theme",
                table: "KhachLinkInstances",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FooterColor",
                table: "KhachLinkInstances");

            migrationBuilder.DropColumn(
                name: "HeaderColor",
                table: "KhachLinkInstances");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "KhachLinkInstances");

            migrationBuilder.DropColumn(
                name: "NavColor",
                table: "KhachLinkInstances");

            migrationBuilder.DropColumn(
                name: "Theme",
                table: "KhachLinkInstances");
        }
    }
}

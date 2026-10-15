using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKhachLinkInstanceIcon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IconContentType",
                table: "KhachLinkInstances",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "IconData",
                table: "KhachLinkInstances",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IconUpdatedAt",
                table: "KhachLinkInstances",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IconContentType",
                table: "KhachLinkInstances");

            migrationBuilder.DropColumn(
                name: "IconData",
                table: "KhachLinkInstances");

            migrationBuilder.DropColumn(
                name: "IconUpdatedAt",
                table: "KhachLinkInstances");
        }
    }
}

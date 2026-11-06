using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.TenantAi.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeSanitized : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "sanitized",
                table: "knowledge_documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "sanitized",
                table: "knowledge_chunks",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sanitized",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "sanitized",
                table: "knowledge_chunks");
        }
    }
}

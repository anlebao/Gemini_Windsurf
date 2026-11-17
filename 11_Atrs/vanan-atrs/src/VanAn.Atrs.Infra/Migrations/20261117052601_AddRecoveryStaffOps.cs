using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.Atrs.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddRecoveryStaffOps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "assigned_at",
                table: "recovery_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "assigned_reviewer_id",
                table: "recovery_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "recovery_case_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recovery_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note_type = table.Column<int>(type: "integer", nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recovery_case_notes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_recovery_case_notes_request",
                table: "recovery_case_notes",
                column: "recovery_request_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recovery_case_notes");

            migrationBuilder.DropColumn(
                name: "assigned_at",
                table: "recovery_requests");

            migrationBuilder.DropColumn(
                name: "assigned_reviewer_id",
                table: "recovery_requests");
        }
    }
}

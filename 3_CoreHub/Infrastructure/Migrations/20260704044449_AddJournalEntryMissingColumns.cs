using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalEntryMissingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EntryDate",
                table: "JournalEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsReversal",
                table: "JournalEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferenceId",
                table: "JournalEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_TenantId_EntryDate",
                table: "JournalEntries",
                columns: new[] { "TenantId", "EntryDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_TenantId_EntryDate",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "EntryDate",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "IsReversal",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "ReferenceId",
                table: "JournalEntries");
        }
    }
}

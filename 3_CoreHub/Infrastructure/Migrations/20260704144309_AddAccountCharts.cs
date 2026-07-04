using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountCharts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountCharts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AccountName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Standard = table.Column<int>(type: "INTEGER", nullable: false),
                    IsNormalCredit = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountCharts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountCharts_Standard",
                table: "AccountCharts",
                column: "Standard");

            migrationBuilder.CreateIndex(
                name: "IX_AccountCharts_Standard_AccountCode",
                table: "AccountCharts",
                columns: new[] { "Standard", "AccountCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountCharts");
        }
    }
}

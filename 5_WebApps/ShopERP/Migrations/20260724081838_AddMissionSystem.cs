using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.ShopERP.Migrations
{
    /// <inheritdoc />
    public partial class AddMissionSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Birthday",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FacebookShareCount",
                table: "Customers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "OtpVerifiedAt",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PWAInstalledAt",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TikTokShareCount",
                table: "Customers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Missions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MissionType = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    PointsReward = table.Column<int>(type: "INTEGER", nullable: false),
                    IsOneTime = table.Column<bool>(type: "INTEGER", nullable: false),
                    DailyCap = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Config = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Missions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MissionCompletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MissionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PointsAwarded = table.Column<int>(type: "INTEGER", nullable: false),
                    Metadata = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionCompletions_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MissionCompletions_CustomerId",
                table: "MissionCompletions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_MissionCompletions_CustomerId_MissionId_CompletedAt",
                table: "MissionCompletions",
                columns: new[] { "CustomerId", "MissionId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MissionCompletions_MissionId",
                table: "MissionCompletions",
                column: "MissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Missions_TenantId_IsActive",
                table: "Missions",
                columns: new[] { "TenantId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MissionCompletions");

            migrationBuilder.DropTable(
                name: "Missions");

            migrationBuilder.DropColumn(
                name: "Birthday",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "FacebookShareCount",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "OtpVerifiedAt",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PWAInstalledAt",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TikTokShareCount",
                table: "Customers");
        }
    }
}

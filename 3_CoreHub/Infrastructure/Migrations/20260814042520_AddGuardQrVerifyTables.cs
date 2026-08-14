using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGuardQrVerifyTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // #126: Guard QR Verify — 2 new tables only.
            // NOTE: Pre-existing Npgsql DateTime column type drift (timestamp without/with time zone)
            // is intentionally NOT included here to keep this migration additive-only and safe.
            // That drift is a separate concern and will be addressed in a dedicated migration if needed.

            migrationBuilder.CreateTable(
                name: "VehicleSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlateNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PlatePhotoKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CustomerPhotoKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    QrTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ShortCode = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IssuedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClaimedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckedOutBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CheckedOutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FlagReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuardScanLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScannedQrTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MatchResult = table.Column<int>(type: "integer", nullable: false),
                    ScannedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ScannedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuardScanLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuardScanLogs_VehicleSessions_VehicleSessionId",
                        column: x => x.VehicleSessionId,
                        principalTable: "VehicleSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuardScanLogs_TenantId_ScannedAt",
                table: "GuardScanLogs",
                columns: new[] { "TenantId", "ScannedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GuardScanLogs_VehicleSessionId",
                table: "GuardScanLogs",
                column: "VehicleSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleSessions_CustomerId",
                table: "VehicleSessions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleSessions_IssuedAt",
                table: "VehicleSessions",
                column: "IssuedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleSessions_TenantId_QrTokenHash",
                table: "VehicleSessions",
                columns: new[] { "TenantId", "QrTokenHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleSessions_TenantId_ShortCode_IssuedAt",
                table: "VehicleSessions",
                columns: new[] { "TenantId", "ShortCode", "IssuedAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleSessions_TenantId_Status",
                table: "VehicleSessions",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuardScanLogs");

            migrationBuilder.DropTable(
                name: "VehicleSessions");
        }
    }
}

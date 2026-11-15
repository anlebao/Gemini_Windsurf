using System;
using Microsoft.EntityFrameworkCore.Migrations;
#pragma warning disable CA1861

#nullable disable

namespace VanAn.Atrs.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddRecoveryCodesAndRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recovery_code_sets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trust_subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    regenerated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recovery_code_sets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recovery_codes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    used_from_device_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recovery_codes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recovery_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trust_subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_type = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    new_device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    evidence_summary_json = table.Column<string>(type: "TEXT", nullable: true),
                    risk_decision_json = table.Column<string>(type: "TEXT", nullable: true),
                    reviewer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decision_reason_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recovery_requests", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_recovery_code_sets_subject_status",
                table: "recovery_code_sets",
                columns: new[] { "trust_subject_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_recovery_codes_set",
                table: "recovery_codes",
                column: "set_id");

            migrationBuilder.CreateIndex(
                name: "ix_recovery_requests_subject_status",
                table: "recovery_requests",
                columns: new[] { "trust_subject_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_recovery_requests_idempotency",
                table: "recovery_requests",
                column: "idempotency_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recovery_code_sets");

            migrationBuilder.DropTable(
                name: "recovery_codes");

            migrationBuilder.DropTable(
                name: "recovery_requests");
        }
    }
}

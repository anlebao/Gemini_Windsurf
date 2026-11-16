using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.Atrs.Infra.Migrations
{
    /// <inheritdoc />
    public partial class DisputeCases_StaffGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dispute_cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trust_subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    case_type = table.Column<int>(type: "integer", nullable: false),
                    related_transaction_ids_json = table.Column<string>(type: "TEXT", nullable: false),
                    related_ledger_references_json = table.Column<string>(type: "TEXT", nullable: false),
                    claimed_issue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    assigned_reviewer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    withdrawn_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    user_facing_status_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dispute_cases", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dispute_decisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispute_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision = table.Column<int>(type: "integer", nullable: false),
                    decision_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    user_facing_explanation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    internal_rationale = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    policy_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    proposed_adjustment_points = table.Column<int>(type: "integer", nullable: true),
                    proposed_adjustment_tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    proposed_adjustment_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    proposed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    rejected_by = table.Column<Guid>(type: "uuid", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    proposed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    decided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    executed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolution_reference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dispute_decisions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dispute_evidence_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispute_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence_type = table.Column<int>(type: "integer", nullable: false),
                    source_system = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_reference = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    captured_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    integrity_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    submitted_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    reliability_class = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    access_classification = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dispute_evidence_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "staff_permission_grants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    demo_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    scope_tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    granted_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_permission_grants", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dispute_cases_idempotency",
                table: "dispute_cases",
                column: "idempotency_key");

            migrationBuilder.CreateIndex(
                name: "ix_dispute_cases_status_submitted",
                table: "dispute_cases",
                columns: new[] { "status", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_dispute_cases_subject_status",
                table: "dispute_cases",
                columns: new[] { "trust_subject_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_dispute_cases_tenant",
                table: "dispute_cases",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_dispute_decisions_case",
                table: "dispute_decisions",
                column: "dispute_case_id");

            migrationBuilder.CreateIndex(
                name: "ux_dispute_decisions_idempotency",
                table: "dispute_decisions",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dispute_evidence_case",
                table: "dispute_evidence_items",
                column: "dispute_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_permission_grants_user_perm",
                table: "staff_permission_grants",
                columns: new[] { "demo_user_id", "permission" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dispute_cases");

            migrationBuilder.DropTable(
                name: "dispute_decisions");

            migrationBuilder.DropTable(
                name: "dispute_evidence_items");

            migrationBuilder.DropTable(
                name: "staff_permission_grants");
        }
    }
}

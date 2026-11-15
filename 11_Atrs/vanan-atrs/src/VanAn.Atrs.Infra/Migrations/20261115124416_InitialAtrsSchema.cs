using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.Atrs.Infra.Migrations
{
    /// <inheritdoc />
    public partial class InitialAtrsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_security_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trust_subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actor_type = table.Column<int>(type: "integer", nullable: false),
                    actor_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    source_application = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    session_correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    outcome = table.Column<int>(type: "integer", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    evidence_references_json = table.Column<string>(type: "TEXT", nullable: true),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    minimal_metadata_json = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account_security_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "account_trust_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trust_subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    assurance_state = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account_trust_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "consent_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trust_subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    privacy_notice_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    withdrawn_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    evidence_ref = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consent_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trust_subject_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trust_subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    identity_type = table.Column<int>(type: "integer", nullable: false),
                    identity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trust_subject_links", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trust_subjects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    primary_contact_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trust_subjects", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_account_security_events_correlation",
                table: "account_security_events",
                column: "session_correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_account_security_events_subject_occurred",
                table: "account_security_events",
                columns: new[] { "trust_subject_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_account_security_events_type_occurred",
                table: "account_security_events",
                columns: new[] { "event_type", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ux_account_trust_profiles_subject",
                table: "account_trust_profiles",
                column: "trust_subject_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_consent_records_subject_purpose",
                table: "consent_records",
                columns: new[] { "trust_subject_id", "purpose" });

            migrationBuilder.CreateIndex(
                name: "ux_trust_subject_links_subject_identity",
                table: "trust_subject_links",
                columns: new[] { "trust_subject_id", "identity_type", "identity_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trust_subjects_contact_hash",
                table: "trust_subjects",
                column: "primary_contact_hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_security_events");

            migrationBuilder.DropTable(
                name: "account_trust_profiles");

            migrationBuilder.DropTable(
                name: "consent_records");

            migrationBuilder.DropTable(
                name: "trust_subject_links");

            migrationBuilder.DropTable(
                name: "trust_subjects");
        }
    }
}

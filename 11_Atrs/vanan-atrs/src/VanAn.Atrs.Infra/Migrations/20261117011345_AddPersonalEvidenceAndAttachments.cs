using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.Atrs.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalEvidenceAndAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evidence_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispute_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trust_subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evidence_attachments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "personal_evidence_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trust_subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    source = table.Column<int>(type: "integer", nullable: false),
                    verification_status = table.Column<int>(type: "integer", nullable: false),
                    privacy_notice_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    consent_record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    collected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personal_evidence_items", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_evidence_attachments_case",
                table: "evidence_attachments",
                column: "dispute_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_attachments_subject",
                table: "evidence_attachments",
                column: "trust_subject_id");

            migrationBuilder.CreateIndex(
                name: "ux_personal_evidence_subject_field",
                table: "personal_evidence_items",
                columns: new[] { "trust_subject_id", "field_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evidence_attachments");

            migrationBuilder.DropTable(
                name: "personal_evidence_items");
        }
    }
}

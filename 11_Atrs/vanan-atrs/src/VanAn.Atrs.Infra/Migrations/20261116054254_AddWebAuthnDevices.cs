using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.Atrs.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddWebAuthnDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_credentials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trust_subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    credential_id = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    public_key = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    sign_count = table.Column<long>(type: "bigint", nullable: false),
                    rp_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    aaguid = table.Column<Guid>(type: "uuid", nullable: true),
                    transports_json = table.Column<string>(type: "TEXT", nullable: true),
                    backup_eligible = table.Column<bool>(type: "boolean", nullable: true),
                    backup_state = table.Column<bool>(type: "boolean", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_credentials", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trust_subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    device_type = table.Column<int>(type: "integer", nullable: false),
                    platform = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    friendly_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    registration_method = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    first_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    risk_metadata_json = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_devices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "webauthn_challenges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trust_subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<int>(type: "integer", nullable: false),
                    challenge = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    origin = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    rp_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    device_record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    options_json = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_webauthn_challenges", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_device_credentials_device",
                table: "device_credentials",
                column: "device_record_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_credentials_subject_status",
                table: "device_credentials",
                columns: new[] { "trust_subject_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_device_credentials_credential_id",
                table: "device_credentials",
                column: "credential_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_devices_last_seen",
                table: "user_devices",
                column: "last_seen_at");

            migrationBuilder.CreateIndex(
                name: "ix_user_devices_subject_status",
                table: "user_devices",
                columns: new[] { "trust_subject_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_user_devices_subject_device",
                table: "user_devices",
                columns: new[] { "trust_subject_id", "device_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_webauthn_challenges_expires",
                table: "webauthn_challenges",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_webauthn_challenges_subject_purpose_consumed",
                table: "webauthn_challenges",
                columns: new[] { "trust_subject_id", "purpose", "consumed_at" });

            migrationBuilder.CreateIndex(
                name: "ux_webauthn_challenges_challenge",
                table: "webauthn_challenges",
                column: "challenge",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_credentials");

            migrationBuilder.DropTable(
                name: "user_devices");

            migrationBuilder.DropTable(
                name: "webauthn_challenges");
        }
    }
}

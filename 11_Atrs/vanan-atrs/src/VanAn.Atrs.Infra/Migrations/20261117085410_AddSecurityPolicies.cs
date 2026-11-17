using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.Atrs.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "security_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    policy_group = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    json_value = table.Column<string>(type: "TEXT", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    proposed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    proposed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    change_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_security_policies", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_security_policies_key_status",
                table: "security_policies",
                columns: new[] { "policy_key", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_security_policies_key_version",
                table: "security_policies",
                columns: new[] { "policy_key", "version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "security_policies");
        }
    }
}

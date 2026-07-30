using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CollaboratorSmsOtp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CC-S6-T5: Add SMS OTP phone verification fields to CommunityRoles
            migrationBuilder.AddColumn<bool>(
                name: "IsPhoneVerified",
                table: "CommunityRoles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PhoneVerifiedAt",
                table: "CommunityRoles",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhoneVerifiedAt",
                table: "CommunityRoles");

            migrationBuilder.DropColumn(
                name: "IsPhoneVerified",
                table: "CommunityRoles");
        }
    }
}

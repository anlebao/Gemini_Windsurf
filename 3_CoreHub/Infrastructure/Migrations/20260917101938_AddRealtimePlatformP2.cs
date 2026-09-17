using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRealtimePlatformP2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Conversations_OrderId",
                table: "Conversations");

            migrationBuilder.AddColumn<Guid>(
                name: "SubjectId",
                table: "DeliveryTrackings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "SubjectType",
                table: "DeliveryTrackings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Delivery");

            migrationBuilder.AddColumn<Guid>(
                name: "TrackerId",
                table: "DeliveryTrackings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubjectId",
                table: "Conversations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "SubjectType",
                table: "Conversations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Order");

            // Backfill SubjectId for existing rows BEFORE the unique (TenantId, SubjectType, SubjectId)
            // index is created — every pre-existing row would otherwise carry the column default
            // (00000000-…) and a tenant with 2+ conversations would violate the index.
            // Conversations were 1-per-order; delivery pings were 1-per-DeliveryTask.
            migrationBuilder.Sql(
                "UPDATE \"Conversations\" SET \"SubjectId\" = \"OrderId\" WHERE \"SubjectId\" = '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql(
                "UPDATE \"DeliveryTrackings\" SET \"SubjectId\" = \"DeliveryTaskId\" WHERE \"SubjectId\" = '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.CreateTable(
                name: "ConversationParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationParticipants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryTrackings_TenantId_SubjectType_SubjectId_RecordedAt",
                table: "DeliveryTrackings",
                columns: new[] { "TenantId", "SubjectType", "SubjectId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_OrderId",
                table: "Conversations",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TenantId_SubjectType_SubjectId",
                table: "Conversations",
                columns: new[] { "TenantId", "SubjectType", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipants_ConversationId_ParticipantId",
                table: "ConversationParticipants",
                columns: new[] { "ConversationId", "ParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipants_ParticipantId",
                table: "ConversationParticipants",
                column: "ParticipantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationParticipants");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryTrackings_TenantId_SubjectType_SubjectId_RecordedAt",
                table: "DeliveryTrackings");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_OrderId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_TenantId_SubjectType_SubjectId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                table: "DeliveryTrackings");

            migrationBuilder.DropColumn(
                name: "SubjectType",
                table: "DeliveryTrackings");

            migrationBuilder.DropColumn(
                name: "TrackerId",
                table: "DeliveryTrackings");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "SubjectType",
                table: "Conversations");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_OrderId",
                table: "Conversations",
                column: "OrderId",
                unique: true);
        }
    }
}

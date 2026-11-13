using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ShopPerCustomerThreads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Conversations_TenantId_SubjectType_SubjectId",
                table: "Conversations");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TenantId_SubjectType_SubjectId_CustomerId",
                table: "Conversations",
                columns: new[] { "TenantId", "SubjectType", "SubjectId", "CustomerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Conversations_TenantId_SubjectType_SubjectId_CustomerId",
                table: "Conversations");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TenantId_SubjectType_SubjectId",
                table: "Conversations",
                columns: new[] { "TenantId", "SubjectType", "SubjectId" },
                unique: true);
        }
    }
}

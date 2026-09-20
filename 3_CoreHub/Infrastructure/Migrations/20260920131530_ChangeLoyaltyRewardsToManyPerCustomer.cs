using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeLoyaltyRewardsToManyPerCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoyaltyRewards_CustomerId",
                table: "LoyaltyRewards");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyRewards_CustomerId",
                table: "LoyaltyRewards",
                column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoyaltyRewards_CustomerId",
                table: "LoyaltyRewards");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyRewards_CustomerId",
                table: "LoyaltyRewards",
                column: "CustomerId",
                unique: true);
        }
    }
}

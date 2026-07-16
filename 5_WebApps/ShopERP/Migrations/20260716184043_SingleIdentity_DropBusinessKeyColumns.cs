using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.ShopERP.Migrations
{
    /// <inheritdoc />
    public partial class SingleIdentity_DropBusinessKeyColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SINGLE-IDENTITY: Align Id = BusinessKey before dropping columns.
            // SQLite: PRAGMA foreign_keys cannot be changed inside a transaction.
            // EF Core migration wraps everything in a transaction, so we use
            // a different approach: update child FKs FIRST (while parent Ids are still old),
            // then update parent Ids, then drop columns.
            // Since we're aligning Id = BusinessKey, and child FKs already reference Id,
            // we need to: 1) set child FKs to BusinessKey value, 2) update parent Id = BusinessKey.
            // This way FKs match after parent Id changes.

            // Step 1: Update child FKs to BusinessKey value (will match new parent Id after step 2)
            // Orders.CustomerId → set to Customers.CustomerId (which will become Customers.Id)
            migrationBuilder.Sql("UPDATE Orders SET CustomerId = (SELECT c.CustomerId FROM Customers c WHERE c.Id = Orders.CustomerId) WHERE CustomerId IS NOT NULL");
            migrationBuilder.Sql("UPDATE LoyaltyRewards SET CustomerId = (SELECT c.CustomerId FROM Customers c WHERE c.Id = LoyaltyRewards.CustomerId)");
            migrationBuilder.Sql("UPDATE OrderItems SET ProductId = (SELECT p.ProductId FROM Products p WHERE p.Id = OrderItems.ProductId)");
            migrationBuilder.Sql("UPDATE Recipes SET ProductId = (SELECT p.ProductId FROM Products p WHERE p.Id = Recipes.ProductId)");
            migrationBuilder.Sql("UPDATE Recipes SET IngredientId = (SELECT i.IngredientId FROM Ingredients i WHERE i.Id = Recipes.IngredientId)");
            migrationBuilder.Sql("UPDATE Inventories SET IngredientId = (SELECT i.IngredientId FROM Ingredients i WHERE i.Id = Inventories.IngredientId)");

            // Step 2: Align parent Id = BusinessKey (now child FKs already point to BusinessKey values)
            migrationBuilder.Sql("UPDATE Products SET Id = ProductId WHERE Id != ProductId");
            migrationBuilder.Sql("UPDATE Customers SET Id = CustomerId WHERE Id != CustomerId");
            migrationBuilder.Sql("UPDATE OrderItems SET Id = OrderItemId WHERE Id != OrderItemId");
            migrationBuilder.Sql("UPDATE Ingredients SET Id = IngredientId WHERE Id != IngredientId");
            migrationBuilder.Sql("UPDATE Recipes SET Id = RecipeId WHERE Id != RecipeId");

            // Step 3: Drop indexes and columns
            migrationBuilder.DropIndex(
                name: "IX_Recipes_RecipeId",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_Products_ProductId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_OrderItemId",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_Ingredients_IngredientId",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "RecipeId",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "OrderItemId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "IngredientId",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Customers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RecipeId",
                table: "Recipes",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                table: "Products",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrderItemId",
                table: "OrderItems",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "IngredientId",
                table: "Ingredients",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "Customers",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_RecipeId",
                table: "Recipes",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_ProductId",
                table: "Products",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderItemId",
                table: "OrderItems",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_IngredientId",
                table: "Ingredients",
                column: "IngredientId");
        }
    }
}

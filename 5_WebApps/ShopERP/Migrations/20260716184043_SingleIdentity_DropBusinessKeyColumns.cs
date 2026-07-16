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
            // PRAGMA foreign_keys=OFF is set in Program.cs BEFORE MigrateAsync
            // (SQLite PRAGMA cannot be changed inside a transaction).

            // Align Id = BusinessKey for all affected entities
            migrationBuilder.Sql("UPDATE Products SET Id = ProductId WHERE Id != ProductId");
            migrationBuilder.Sql("UPDATE Customers SET Id = CustomerId WHERE Id != CustomerId");
            migrationBuilder.Sql("UPDATE OrderItems SET Id = OrderItemId WHERE Id != OrderItemId");
            migrationBuilder.Sql("UPDATE Ingredients SET Id = IngredientId WHERE Id != IngredientId");
            migrationBuilder.Sql("UPDATE Recipes SET Id = RecipeId WHERE Id != RecipeId");

            // Update child table FKs to match new parent Ids
            // (parent Ids changed above, child FKs still point to old Ids)
            migrationBuilder.Sql("UPDATE Orders SET CustomerId = (SELECT c.Id FROM Customers c WHERE c.CustomerId = Orders.CustomerId) WHERE CustomerId IS NOT NULL");
            migrationBuilder.Sql("UPDATE LoyaltyRewards SET CustomerId = (SELECT c.Id FROM Customers c WHERE c.CustomerId = LoyaltyRewards.CustomerId)");
            migrationBuilder.Sql("UPDATE OrderItems SET ProductId = (SELECT p.Id FROM Products p WHERE p.ProductId = OrderItems.ProductId)");
            migrationBuilder.Sql("UPDATE Recipes SET ProductId = (SELECT p.Id FROM Products p WHERE p.ProductId = Recipes.ProductId)");
            migrationBuilder.Sql("UPDATE Recipes SET IngredientId = (SELECT i.Id FROM Ingredients i WHERE i.IngredientId = Recipes.IngredientId)");
            migrationBuilder.Sql("UPDATE Inventories SET IngredientId = (SELECT i.Id FROM Ingredients i WHERE i.IngredientId = Inventories.IngredientId)");

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

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SingleIdentity_DropBusinessKeyColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SINGLE-IDENTITY: Align Id = BusinessKey before dropping columns (PostgreSQL).
            // PostgreSQL: use ALTER TABLE DROP CONSTRAINT to remove FKs temporarily,
            // then UPDATE Ids, then recreate FKs pointing to new Ids.
            // This avoids FK violations when changing parent PKs.

            // Drop FK constraints that reference Customers.Id, Products.Id, Ingredients.Id
            migrationBuilder.Sql(@"ALTER TABLE ""Orders"" DROP CONSTRAINT IF EXISTS ""FK_Orders_Customers_CustomerId""");
            migrationBuilder.Sql(@"ALTER TABLE ""LoyaltyRewards"" DROP CONSTRAINT IF EXISTS ""FK_LoyaltyRewards_Customers_CustomerId""");
            migrationBuilder.Sql(@"ALTER TABLE ""OrderItems"" DROP CONSTRAINT IF EXISTS ""FK_OrderItems_Products_ProductId""");
            migrationBuilder.Sql(@"ALTER TABLE ""Recipes"" DROP CONSTRAINT IF EXISTS ""FK_Recipes_Products_ProductId""");
            migrationBuilder.Sql(@"ALTER TABLE ""Recipes"" DROP CONSTRAINT IF EXISTS ""FK_Recipes_Ingredients_IngredientId""");
            migrationBuilder.Sql(@"ALTER TABLE ""Inventories"" DROP CONSTRAINT IF EXISTS ""FK_Inventories_Ingredients_IngredientId""");

            // Align Id = BusinessKey for all affected entities
            migrationBuilder.Sql(@"UPDATE ""Products"" SET ""Id"" = ""ProductId"" WHERE ""Id"" != ""ProductId""");
            migrationBuilder.Sql(@"UPDATE ""Customers"" SET ""Id"" = ""CustomerId"" WHERE ""Id"" != ""CustomerId""");
            migrationBuilder.Sql(@"UPDATE ""OrderItems"" SET ""Id"" = ""OrderItemId"" WHERE ""Id"" != ""OrderItemId""");
            migrationBuilder.Sql(@"UPDATE ""Ingredients"" SET ""Id"" = ""IngredientId"" WHERE ""Id"" != ""IngredientId""");
            migrationBuilder.Sql(@"UPDATE ""Recipes"" SET ""Id"" = ""RecipeId"" WHERE ""Id"" != ""RecipeId""");

            // Update child table FKs to match new parent Ids
            migrationBuilder.Sql(@"UPDATE ""Orders"" SET ""CustomerId"" = (SELECT c.""Id"" FROM ""Customers"" c WHERE c.""CustomerId"" = ""Orders"".""CustomerId"") WHERE ""CustomerId"" IS NOT NULL");
            migrationBuilder.Sql(@"UPDATE ""LoyaltyRewards"" SET ""CustomerId"" = (SELECT c.""Id"" FROM ""Customers"" c WHERE c.""CustomerId"" = ""LoyaltyRewards"".""CustomerId"")");
            migrationBuilder.Sql(@"UPDATE ""OrderItems"" SET ""ProductId"" = (SELECT p.""Id"" FROM ""Products"" p WHERE p.""ProductId"" = ""OrderItems"".""ProductId"")");
            migrationBuilder.Sql(@"UPDATE ""Recipes"" SET ""ProductId"" = (SELECT p.""Id"" FROM ""Products"" p WHERE p.""ProductId"" = ""Recipes"".""ProductId"")");
            migrationBuilder.Sql(@"UPDATE ""Recipes"" SET ""IngredientId"" = (SELECT i.""Id"" FROM ""Ingredients"" i WHERE i.""IngredientId"" = ""Recipes"".""IngredientId"")");
            migrationBuilder.Sql(@"UPDATE ""Inventories"" SET ""IngredientId"" = (SELECT i.""Id"" FROM ""Ingredients"" i WHERE i.""IngredientId"" = ""Inventories"".""IngredientId"")");

            // Recreate FK constraints pointing to Id (PK)
            migrationBuilder.Sql(@"ALTER TABLE ""Orders"" ADD CONSTRAINT ""FK_Orders_Customers_CustomerId"" FOREIGN KEY (""CustomerId"") REFERENCES ""Customers""(""Id"") ON DELETE SET NULL");
            migrationBuilder.Sql(@"ALTER TABLE ""LoyaltyRewards"" ADD CONSTRAINT ""FK_LoyaltyRewards_Customers_CustomerId"" FOREIGN KEY (""CustomerId"") REFERENCES ""Customers""(""Id"") ON DELETE CASCADE");
            migrationBuilder.Sql(@"ALTER TABLE ""OrderItems"" ADD CONSTRAINT ""FK_OrderItems_Products_ProductId"" FOREIGN KEY (""ProductId"") REFERENCES ""Products""(""Id"") ON DELETE RESTRICT");
            migrationBuilder.Sql(@"ALTER TABLE ""Recipes"" ADD CONSTRAINT ""FK_Recipes_Products_ProductId"" FOREIGN KEY (""ProductId"") REFERENCES ""Products""(""Id"") ON DELETE RESTRICT");
            migrationBuilder.Sql(@"ALTER TABLE ""Recipes"" ADD CONSTRAINT ""FK_Recipes_Ingredients_IngredientId"" FOREIGN KEY (""IngredientId"") REFERENCES ""Ingredients""(""Id"") ON DELETE RESTRICT");
            migrationBuilder.Sql(@"ALTER TABLE ""Inventories"" ADD CONSTRAINT ""FK_Inventories_Ingredients_IngredientId"" FOREIGN KEY (""IngredientId"") REFERENCES ""Ingredients""(""Id"") ON DELETE RESTRICT");

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
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                table: "Products",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrderItemId",
                table: "OrderItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "IngredientId",
                table: "Ingredients",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "Customers",
                type: "uuid",
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

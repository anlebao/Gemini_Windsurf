BEGIN TRANSACTION;

CREATE TABLE "ef_temp_Orders" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Orders" PRIMARY KEY,
    "CompletedAt" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "CreatedBy" TEXT NULL,
    "CustomerDeviceId" TEXT NULL,
    "CustomerId" TEXT NULL,
    "CustomerInfo_Address" TEXT NULL,
    "CustomerInfo_Email" TEXT NULL,
    "CustomerInfo_FullName" TEXT NULL,
    "CustomerInfo_Notes" TEXT NULL,
    "CustomerInfo_PhoneNumber" TEXT NULL,
    "CustomerNotes" TEXT NULL,
    "DeliveryAddress" TEXT NULL,
    "DiscountAmount" TEXT NOT NULL,
    "IndustrySector" INTEGER NULL,
    "IsDeleted" INTEGER NOT NULL,
    "IsSyncedToCoreHub" INTEGER NOT NULL,
    "KitchenStatus" INTEGER NOT NULL,
    "LastSyncedAt" TEXT NULL,
    "Notes" TEXT NULL,
    "OrderDate" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    "OrderType" TEXT NOT NULL DEFAULT 'DINEIN',
    "PaymentMethod" TEXT NULL,
    "PaymentStatus" TEXT NULL,
    "ShippingFee" TEXT NOT NULL,
    "StaffNotes" TEXT NULL,
    "Status" TEXT NOT NULL,
    "SubTotal" TEXT NOT NULL,
    "TenantId" TEXT NOT NULL,
    "TextCommand" TEXT NULL,
    "TotalAmount" TEXT NOT NULL,
    "TotalVatAmount" TEXT NOT NULL,
    "TrackingCode" TEXT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "UpdatedBy" TEXT NULL,
    "VietQR_Payload" TEXT NULL,
    "VietQR_TransactionId" TEXT NULL,
    "VoiceCommandUrl" TEXT NULL,
    "VoiceNoteAudioBlob" TEXT NULL,
    "VoiceNoteText" TEXT NULL,
    CONSTRAINT "FK_Orders_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE SET NULL
);

INSERT INTO "ef_temp_Orders" ("Id", "CompletedAt", "CreatedAt", "CreatedBy", "CustomerDeviceId", "CustomerId", "CustomerInfo_Address", "CustomerInfo_Email", "CustomerInfo_FullName", "CustomerInfo_Notes", "CustomerInfo_PhoneNumber", "CustomerNotes", "DeliveryAddress", "DiscountAmount", "IndustrySector", "IsDeleted", "IsSyncedToCoreHub", "KitchenStatus", "LastSyncedAt", "Notes", "OrderDate", "OrderType", "PaymentMethod", "PaymentStatus", "ShippingFee", "StaffNotes", "Status", "SubTotal", "TenantId", "TextCommand", "TotalAmount", "TotalVatAmount", "TrackingCode", "UpdatedAt", "UpdatedBy", "VietQR_Payload", "VietQR_TransactionId", "VoiceCommandUrl", "VoiceNoteAudioBlob", "VoiceNoteText")
SELECT "Id", "CompletedAt", "CreatedAt", "CreatedBy", "CustomerDeviceId", "CustomerId", "CustomerInfo_Address", "CustomerInfo_Email", "CustomerInfo_FullName", "CustomerInfo_Notes", "CustomerInfo_PhoneNumber", "CustomerNotes", "DeliveryAddress", "DiscountAmount", "IndustrySector", "IsDeleted", "IsSyncedToCoreHub", "KitchenStatus", "LastSyncedAt", "Notes", "OrderDate", "OrderType", "PaymentMethod", "PaymentStatus", "ShippingFee", "StaffNotes", "Status", "SubTotal", "TenantId", "TextCommand", "TotalAmount", "TotalVatAmount", "TrackingCode", "UpdatedAt", "UpdatedBy", "VietQR_Payload", "VietQR_TransactionId", "VoiceCommandUrl", "VoiceNoteAudioBlob", "VoiceNoteText"
FROM "Orders";

COMMIT;

PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;

DROP TABLE "Orders";

ALTER TABLE "ef_temp_Orders" RENAME TO "Orders";

COMMIT;

PRAGMA foreign_keys = 1;

BEGIN TRANSACTION;

CREATE INDEX "IX_Orders_CustomerId" ON "Orders" ("CustomerId");

CREATE INDEX "IX_Orders_OrderDate" ON "Orders" ("OrderDate");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260716082930_DropOrderOrderIdColumn', '8.0.8');

COMMIT;

BEGIN TRANSACTION;

DROP INDEX "IX_Recipes_RecipeId";

DROP INDEX "IX_Products_ProductId";

DROP INDEX "IX_OrderItems_OrderItemId";

DROP INDEX "IX_Ingredients_IngredientId";

CREATE TABLE "ef_temp_Recipes" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Recipes" PRIMARY KEY,
    "CreatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    "CreatedBy" TEXT NULL,
    "IngredientId" TEXT NOT NULL,
    "IsDeleted" INTEGER NOT NULL,
    "ProductId" TEXT NOT NULL,
    "QuantityNeeded" TEXT NOT NULL,
    "TenantId" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "UpdatedBy" TEXT NULL,
    CONSTRAINT "FK_Recipes_Ingredients_IngredientId" FOREIGN KEY ("IngredientId") REFERENCES "Ingredients" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Recipes_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT
);

INSERT INTO "ef_temp_Recipes" ("Id", "CreatedAt", "CreatedBy", "IngredientId", "IsDeleted", "ProductId", "QuantityNeeded", "TenantId", "UpdatedAt", "UpdatedBy")
SELECT "Id", "CreatedAt", "CreatedBy", "IngredientId", "IsDeleted", "ProductId", "QuantityNeeded", "TenantId", "UpdatedAt", "UpdatedBy"
FROM "Recipes";

CREATE TABLE "ef_temp_Products" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Products" PRIMARY KEY,
    "Category" TEXT NOT NULL,
    "CostPrice" TEXT NOT NULL DEFAULT '0.0',
    "CreatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    "CreatedBy" TEXT NULL,
    "Description" TEXT NOT NULL,
    "ImageUrl" TEXT NULL,
    "IsActive" INTEGER NOT NULL,
    "IsDeleted" INTEGER NOT NULL,
    "Name" TEXT NOT NULL,
    "Price" TEXT NOT NULL,
    "TenantId" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "UpdatedBy" TEXT NULL,
    "VatRate" TEXT NOT NULL
);

INSERT INTO "ef_temp_Products" ("Id", "Category", "CostPrice", "CreatedAt", "CreatedBy", "Description", "ImageUrl", "IsActive", "IsDeleted", "Name", "Price", "TenantId", "UpdatedAt", "UpdatedBy", "VatRate")
SELECT "Id", "Category", "CostPrice", "CreatedAt", "CreatedBy", "Description", "ImageUrl", "IsActive", "IsDeleted", "Name", "Price", "TenantId", "UpdatedAt", "UpdatedBy", "VatRate"
FROM "Products";

CREATE TABLE "ef_temp_OrderItems" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_OrderItems" PRIMARY KEY,
    "CreatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    "CreatedBy" TEXT NULL,
    "IsDeleted" INTEGER NOT NULL,
    "ItemNoteAudioBlob" TEXT NULL,
    "ItemNoteText" TEXT NULL,
    "KitchenStatus" INTEGER NOT NULL,
    "Notes" TEXT NULL,
    "OrderId" TEXT NOT NULL,
    "ProductId" TEXT NOT NULL,
    "ProductName" TEXT NOT NULL,
    "Quantity" INTEGER NOT NULL,
    "TenantId" TEXT NOT NULL,
    "UnitPrice" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "UpdatedBy" TEXT NULL,
    "VatRate" TEXT NOT NULL,
    CONSTRAINT "FK_OrderItems_Orders_OrderId" FOREIGN KEY ("OrderId") REFERENCES "Orders" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_OrderItems_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT
);

INSERT INTO "ef_temp_OrderItems" ("Id", "CreatedAt", "CreatedBy", "IsDeleted", "ItemNoteAudioBlob", "ItemNoteText", "KitchenStatus", "Notes", "OrderId", "ProductId", "ProductName", "Quantity", "TenantId", "UnitPrice", "UpdatedAt", "UpdatedBy", "VatRate")
SELECT "Id", "CreatedAt", "CreatedBy", "IsDeleted", "ItemNoteAudioBlob", "ItemNoteText", "KitchenStatus", "Notes", "OrderId", "ProductId", "ProductName", "Quantity", "TenantId", "UnitPrice", "UpdatedAt", "UpdatedBy", "VatRate"
FROM "OrderItems";

CREATE TABLE "ef_temp_Ingredients" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Ingredients" PRIMARY KEY,
    "CreatedAt" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    "CreatedBy" TEXT NULL,
    "CurrentStock" TEXT NOT NULL,
    "IsDeleted" INTEGER NOT NULL,
    "MinStockThreshold" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "PricePerUnit" TEXT NOT NULL,
    "TenantId" TEXT NOT NULL,
    "Unit" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "UpdatedBy" TEXT NULL
);

INSERT INTO "ef_temp_Ingredients" ("Id", "CreatedAt", "CreatedBy", "CurrentStock", "IsDeleted", "MinStockThreshold", "Name", "PricePerUnit", "TenantId", "Unit", "UpdatedAt", "UpdatedBy")
SELECT "Id", "CreatedAt", "CreatedBy", "CurrentStock", "IsDeleted", "MinStockThreshold", "Name", "PricePerUnit", "TenantId", "Unit", "UpdatedAt", "UpdatedBy"
FROM "Ingredients";

CREATE TABLE "ef_temp_Customers" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Customers" PRIMARY KEY,
    "CreatedAt" TEXT NOT NULL,
    "CreatedBy" TEXT NULL,
    "CustomerTier" TEXT NOT NULL,
    "DeviceId" TEXT NULL,
    "Email" TEXT NULL,
    "FullName" TEXT NOT NULL,
    "IdentityLevel" INTEGER NOT NULL,
    "IsActive" INTEGER NOT NULL,
    "IsDeleted" INTEGER NOT NULL,
    "LastOrderDate" TEXT NULL,
    "LoyaltyPoints" INTEGER NOT NULL,
    "PhoneNumber" TEXT NOT NULL,
    "TenantId" TEXT NOT NULL,
    "TotalSpent" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "UpdatedBy" TEXT NULL
);

INSERT INTO "ef_temp_Customers" ("Id", "CreatedAt", "CreatedBy", "CustomerTier", "DeviceId", "Email", "FullName", "IdentityLevel", "IsActive", "IsDeleted", "LastOrderDate", "LoyaltyPoints", "PhoneNumber", "TenantId", "TotalSpent", "UpdatedAt", "UpdatedBy")
SELECT "Id", "CreatedAt", "CreatedBy", "CustomerTier", "DeviceId", "Email", "FullName", "IdentityLevel", "IsActive", "IsDeleted", "LastOrderDate", "LoyaltyPoints", "PhoneNumber", "TenantId", "TotalSpent", "UpdatedAt", "UpdatedBy"
FROM "Customers";

COMMIT;

PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;

DROP TABLE "Recipes";

ALTER TABLE "ef_temp_Recipes" RENAME TO "Recipes";

DROP TABLE "Products";

ALTER TABLE "ef_temp_Products" RENAME TO "Products";

DROP TABLE "OrderItems";

ALTER TABLE "ef_temp_OrderItems" RENAME TO "OrderItems";

DROP TABLE "Ingredients";

ALTER TABLE "ef_temp_Ingredients" RENAME TO "Ingredients";

DROP TABLE "Customers";

ALTER TABLE "ef_temp_Customers" RENAME TO "Customers";

COMMIT;

PRAGMA foreign_keys = 1;

BEGIN TRANSACTION;

CREATE INDEX "IX_Recipes_IngredientId" ON "Recipes" ("IngredientId");

CREATE INDEX "IX_Recipes_ProductId" ON "Recipes" ("ProductId");

CREATE INDEX "IX_Recipes_TenantId_ProductId" ON "Recipes" ("TenantId", "ProductId");

CREATE INDEX "IX_Products_IsActive" ON "Products" ("IsActive");

CREATE INDEX "IX_Products_TenantId_Category" ON "Products" ("TenantId", "Category");

CREATE INDEX "IX_OrderItems_KitchenStatus" ON "OrderItems" ("KitchenStatus");

CREATE INDEX "IX_OrderItems_OrderId" ON "OrderItems" ("OrderId");

CREATE INDEX "IX_OrderItems_ProductId" ON "OrderItems" ("ProductId");

CREATE INDEX "IX_OrderItems_TenantId_OrderId" ON "OrderItems" ("TenantId", "OrderId");

CREATE INDEX "IX_Ingredients_TenantId_Name" ON "Ingredients" ("TenantId", "Name");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260716184043_SingleIdentity_DropBusinessKeyColumns', '8.0.8');

COMMIT;


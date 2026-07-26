using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CommunitySprint0 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Loyalty_AwardOnAllOrders",
                table: "ShopFeatureSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "Loyalty_MaxPointsPerOrder",
                table: "ShopFeatureSettings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Loyalty_MinPointsPerOrder",
                table: "ShopFeatureSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Loyalty_PointsRate",
                table: "ShopFeatureSettings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "Notify_BirthdayBonus",
                table: "ShopFeatureSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "Notify_MissionCompleted",
                table: "ShopFeatureSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "Notify_RedemptionCancelled",
                table: "ShopFeatureSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "Notify_RedemptionFulfilled",
                table: "ShopFeatureSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "Notify_VoucherExpiringSoon",
                table: "ShopFeatureSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "VoucherExpiryNotifyHours",
                table: "ShopFeatureSettings",
                type: "integer",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.AddColumn<decimal>(
                name: "CodAmount",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CodCollectedAt",
                table: "Orders",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DeliveryLat",
                table: "Orders",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DeliveryLng",
                table: "Orders",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferralCode",
                table: "Orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferralProductId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SalesmanId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ShipperId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Birthday",
                table: "Customers",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FacebookShareCount",
                table: "Customers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "OtpVerifiedAt",
                table: "Customers",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PWAInstalledAt",
                table: "Customers",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TikTokShareCount",
                table: "Customers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AppInstallAttributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesmanId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesReferralId = table.Column<Guid>(type: "uuid", nullable: true),
                    BonusAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AttributionStatus = table.Column<int>(type: "integer", nullable: false),
                    InstalledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    WalletTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RiskScore = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RiskFactors = table.Column<string>(type: "text", nullable: true),
                    HoldUntil = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DeviceRegistrationId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppInstallAttributions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunityRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleType = table.Column<int>(type: "integer", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ActivatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    DeactivatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SalesmanCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipperId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipperId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PickedUpAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    OutForDeliveryAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ShopLat = table.Column<double>(type: "double precision", nullable: false),
                    ShopLng = table.Column<double>(type: "double precision", nullable: false),
                    CustomerLat = table.Column<double>(type: "double precision", nullable: true),
                    CustomerLng = table.Column<double>(type: "double precision", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryTrackings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryTrackings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FingerprintHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FingerprintSignals = table.Column<string>(type: "text", nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RiskScore = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceRegistrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FraudFlags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<int>(type: "integer", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    FlagType = table.Column<int>(type: "integer", nullable: false),
                    RiskScore = table.Column<int>(type: "integer", nullable: false),
                    RiskFactors = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FraudFlags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Missions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionType = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PointsReward = table.Column<int>(type: "integer", nullable: false),
                    IsOneTime = table.Column<bool>(type: "boolean", nullable: false),
                    DailyCap = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Config = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Missions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductReferralConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductShortCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CommissionRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AppInstallBonus = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_ProductReferralConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RedemptionCatalogItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PointsRequired = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    StockCount = table.Column<int>(type: "integer", nullable: true),
                    ValidFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    VoucherExpiryDays = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RedemptionCatalogItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RedemptionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: true),
                    PointsSpent = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    RedeemedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FulfilledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RedemptionRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesReferrals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesmanId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesmanCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductShortCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ReferredCustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommissionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CommissionRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CommissionStatus = table.Column<int>(type: "integer", nullable: false),
                    AppInstallBonusAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AppInstallBonusStatus = table.Column<int>(type: "integer", nullable: false),
                    AppInstallAttributionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RiskScore = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RiskFactors = table.Column<string>(type: "text", nullable: true),
                    HoldUntil = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesReferrals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vouchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RedemptionRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    QRCodeData = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    IssuedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vouchers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WalletTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RelatedOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    BalanceAfter = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MissionCompletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PointsAwarded = table.Column<int>(type: "integer", nullable: false),
                    Metadata = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionCompletions_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ReferralProductId",
                table: "Orders",
                column: "ReferralProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_SalesmanId",
                table: "Orders",
                column: "SalesmanId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ShipperId",
                table: "Orders",
                column: "ShipperId");

            migrationBuilder.CreateIndex(
                name: "IX_AppInstallAttributions_AttributionStatus",
                table: "AppInstallAttributions",
                column: "AttributionStatus");

            migrationBuilder.CreateIndex(
                name: "IX_AppInstallAttributions_CustomerId",
                table: "AppInstallAttributions",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppInstallAttributions_ProductId",
                table: "AppInstallAttributions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_AppInstallAttributions_SalesmanId",
                table: "AppInstallAttributions",
                column: "SalesmanId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityRoles_CustomerId_RoleType_IsActive",
                table: "CommunityRoles",
                columns: new[] { "CustomerId", "RoleType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityRoles_SalesmanCode",
                table: "CommunityRoles",
                column: "SalesmanCode",
                unique: true,
                filter: "\"SalesmanCode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_OrderId",
                table: "Conversations",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryTasks_OrderId",
                table: "DeliveryTasks",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryTasks_OrderId_Status",
                table: "DeliveryTasks",
                columns: new[] { "OrderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryTasks_ShipperId",
                table: "DeliveryTasks",
                column: "ShipperId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryTrackings_DeliveryTaskId_RecordedAt",
                table: "DeliveryTrackings",
                columns: new[] { "DeliveryTaskId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceRegistrations_CustomerId_IsActive",
                table: "DeviceRegistrations",
                columns: new[] { "CustomerId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceRegistrations_DeviceToken",
                table: "DeviceRegistrations",
                column: "DeviceToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceRegistrations_FingerprintHash",
                table: "DeviceRegistrations",
                column: "FingerprintHash");

            migrationBuilder.CreateIndex(
                name: "IX_FraudFlags_CustomerId",
                table: "FraudFlags",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_FraudFlags_EntityType_EntityId",
                table: "FraudFlags",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_FraudFlags_Status_CreatedAt",
                table: "FraudFlags",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId",
                table: "Messages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_MissionCompletions_CustomerId",
                table: "MissionCompletions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_MissionCompletions_CustomerId_MissionId_CompletedAt",
                table: "MissionCompletions",
                columns: new[] { "CustomerId", "MissionId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MissionCompletions_MissionId",
                table: "MissionCompletions",
                column: "MissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Missions_TenantId_IsActive",
                table: "Missions",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductReferralConfigs_ProductId",
                table: "ProductReferralConfigs",
                column: "ProductId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductReferralConfigs_TenantId_ProductShortCode",
                table: "ProductReferralConfigs",
                columns: new[] { "TenantId", "ProductShortCode" },
                unique: true,
                filter: "\"ProductShortCode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RedemptionCatalogItems_PointsRequired",
                table: "RedemptionCatalogItems",
                column: "PointsRequired");

            migrationBuilder.CreateIndex(
                name: "IX_RedemptionCatalogItems_TenantId_IsActive",
                table: "RedemptionCatalogItems",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_RedemptionRecords_CatalogItemId",
                table: "RedemptionRecords",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RedemptionRecords_CustomerId",
                table: "RedemptionRecords",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_RedemptionRecords_TenantId_Status",
                table: "RedemptionRecords",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RedemptionRecords_VoucherId",
                table: "RedemptionRecords",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReferrals_CommissionStatus",
                table: "SalesReferrals",
                column: "CommissionStatus");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReferrals_OrderId",
                table: "SalesReferrals",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReferrals_ProductId",
                table: "SalesReferrals",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReferrals_SalesmanCode",
                table: "SalesReferrals",
                column: "SalesmanCode");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_CustomerId",
                table: "Vouchers",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_RedemptionRecordId",
                table: "Vouchers",
                column: "RedemptionRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_TenantId_Status",
                table: "Vouchers",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_VoucherCode",
                table: "Vouchers",
                column: "VoucherCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_OwnerId",
                table: "WalletTransactions",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_RelatedOrderId",
                table: "WalletTransactions",
                column: "RelatedOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_RelatedTransactionId",
                table: "WalletTransactions",
                column: "RelatedTransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppInstallAttributions");

            migrationBuilder.DropTable(
                name: "CommunityRoles");

            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.DropTable(
                name: "DeliveryTasks");

            migrationBuilder.DropTable(
                name: "DeliveryTrackings");

            migrationBuilder.DropTable(
                name: "DeviceRegistrations");

            migrationBuilder.DropTable(
                name: "FraudFlags");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "MissionCompletions");

            migrationBuilder.DropTable(
                name: "ProductReferralConfigs");

            migrationBuilder.DropTable(
                name: "RedemptionCatalogItems");

            migrationBuilder.DropTable(
                name: "RedemptionRecords");

            migrationBuilder.DropTable(
                name: "SalesReferrals");

            migrationBuilder.DropTable(
                name: "Vouchers");

            migrationBuilder.DropTable(
                name: "WalletTransactions");

            migrationBuilder.DropTable(
                name: "Missions");

            migrationBuilder.DropIndex(
                name: "IX_Orders_ReferralProductId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_SalesmanId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_ShipperId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Loyalty_AwardOnAllOrders",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Loyalty_MaxPointsPerOrder",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Loyalty_MinPointsPerOrder",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Loyalty_PointsRate",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Notify_BirthdayBonus",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Notify_MissionCompleted",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Notify_RedemptionCancelled",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Notify_RedemptionFulfilled",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "Notify_VoucherExpiringSoon",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "VoucherExpiryNotifyHours",
                table: "ShopFeatureSettings");

            migrationBuilder.DropColumn(
                name: "CodAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CodCollectedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryLat",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryLng",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReferralCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReferralProductId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SalesmanId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShipperId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Birthday",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "FacebookShareCount",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "OtpVerifiedAt",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PWAInstalledAt",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TikTokShareCount",
                table: "Customers");
        }
    }
}

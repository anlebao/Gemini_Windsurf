using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMembershipInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE (2026-09-29): Pre-existing model drift "Customers.IdentityLevel default" (model vs snapshot
            // lệch từ trước — sẽ xuất hiện ở MỌI migration add). Đã LOẠI khỏi migration này để giữ thuần additive;
            // cleanup riêng khi có migration chủ đích (xem task card Phase 2 note).

            migrationBuilder.CreateTable(
                name: "ConsentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantCustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    DocumentVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EvidenceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsentRecords_Customers_ApplicantCustomerId",
                        column: x => x.ApplicantCustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsentRecords_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HtxProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharterVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TermsVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CharterUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HtxProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HtxProfiles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberCustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemberTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    MembershipType = table.Column<int>(type: "integer", nullable: false),
                    MemberNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EffectiveAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TerminatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    StatusReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Members", x => x.Id);
                    table.CheckConstraint("CK_Members_SingleParty", "(\"MemberCustomerId\" IS NULL) <> (\"MemberTenantId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_Members_Customers_MemberCustomerId",
                        column: x => x.MemberCustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Members_Tenants_MemberTenantId",
                        column: x => x.MemberTenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Members_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MembershipApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantCustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    MembershipType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Region = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExpectedRole = table.Column<int>(type: "integer", nullable: false),
                    IdentityVerificationLevel = table.Column<int>(type: "integer", nullable: false),
                    ConsentVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CharterVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CapitalFeeStatus = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NeedInfoReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MembershipApplications_Customers_ApplicantCustomerId",
                        column: x => x.ApplicantCustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MembershipApplications_Tenants_BusinessTenantId",
                        column: x => x.BusinessTenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MembershipApplications_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_ApplicantCustomerId",
                table: "ConsentRecords",
                column: "ApplicantCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_HtxApplicant",
                table: "ConsentRecords",
                columns: new[] { "TenantId", "ApplicantCustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_TenantId",
                table: "ConsentRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_HtxProfiles_TenantId",
                table: "HtxProfiles",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Members_MemberCustomerId",
                table: "Members",
                column: "MemberCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Members_MemberTenantId",
                table: "Members",
                column: "MemberTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Members_TenantId",
                table: "Members",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_Members_HtxCustomer",
                table: "Members",
                columns: new[] { "TenantId", "MemberCustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Members_HtxTenant",
                table: "Members",
                columns: new[] { "TenantId", "MemberTenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Members_MemberNumber",
                table: "Members",
                column: "MemberNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MembershipApplications_ApplicantCustomerId",
                table: "MembershipApplications",
                column: "ApplicantCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipApplications_BusinessTenantId",
                table: "MembershipApplications",
                column: "BusinessTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipApplications_HtxApplicant",
                table: "MembershipApplications",
                columns: new[] { "TenantId", "ApplicantCustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_MembershipApplications_Status",
                table: "MembershipApplications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipApplications_TenantId",
                table: "MembershipApplications",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsentRecords");

            migrationBuilder.DropTable(
                name: "HtxProfiles");

            migrationBuilder.DropTable(
                name: "Members");

            migrationBuilder.DropTable(
                name: "MembershipApplications");
        }
    }
}

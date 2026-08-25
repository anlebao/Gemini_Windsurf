using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.ShopERP.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Crawl-to-Onboard Pipeline (2026-08-25): Add 2 columns to Tenants table for SQLite mirror.
    /// Correction C2: ShopERP SQLite has DbSet<Tenant> (ShopERPDbContext.cs:55) — schema must match PG
    /// for EF model snapshot consistency, even if values not populated in SQLite.
    ///
    /// Columns added:
    /// - PotentialDuplicateOf (Guid?, nullable) — flag for duplicate MST tenants (correction C1: Guid, not TenantId)
    /// - Settings_CrawledPhone (string, maxLength 50, nullable) — raw crawled phone (M3: internal use, NOT displayed)
    ///
    /// Note: TenantClaimRequests + CrawlSources tables are PG-only (Option C) — NOT created in SQLite.
    /// Entities are Ignored in ShopERPDbContext.OnModelCreating.
    ///
    /// Pre-existing drift: TenantDomains table missing from SQLite migrations (separate tech debt,
    /// not addressed here — Domain Reseller R1 tables are PG-only in practice, DbSet exists for
    /// IVanAnDbContext interface contract only). Snapshot includes TenantDomains for model consistency
    /// but no migration creates the table in SQLite — will be addressed in separate tech debt task.
    /// </summary>
    public partial class AddCrawlOnboardingTenantsColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PotentialDuplicateOf",
                table: "Tenants",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Settings_CrawledPhone",
                table: "Tenants",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PotentialDuplicateOf",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Settings_CrawledPhone",
                table: "Tenants");
        }
    }
}

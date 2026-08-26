using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.CoreHub.Infrastructure.ValueConverters;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for the Rich Domain Tenant aggregate (Wave 5).
    /// Maps VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant to the "Tenants" table.
    /// The old anemic record VanAn.Shared.Domain.Tenant is [Obsolete] and no longer mapped.
    /// </summary>
    public class TenantConfiguration : IEntityTypeConfiguration<Tenant>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<Tenant> builder)
        {
            builder.ToTable("Tenants");

            // Primary key: TenantId value object (stored as TEXT/UUID)
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id)
                .HasColumnName("Id")
                .HasConversion(id => id.Value, value => new TenantId(value))
                .IsRequired();

            builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            // BusinessType enum → int
            builder.Property(e => e.BusinessType)
                .HasConversion<int>();

            // HKDGroup nullable enum → int?
            builder.Property(e => e.HKDGroup)
                .HasConversion<int?>();

            // Wave 5 (approved 2026-07-03): Default industry sector for HKD Group 2 tenants.
            // Nullable — existing tenants get NULL, must be set before generating S2a/S2b.
            builder.Property(e => e.DefaultIndustrySector)
                .HasConversion<int?>();

            // Wave 5: TenantStatus enum → int (default Active=1)
            builder.Property(e => e.Status)
                .HasConversion<int>()
                .HasDefaultValue(TenantStatus.Active)
                .IsRequired();

            // Wave 5: TenantSettings owned value object (flattened into Tenants table)
            builder.OwnsOne(e => e.Settings, settings =>
            {
                settings.Property(s => s.ContactEmail).HasColumnName("Settings_ContactEmail").HasMaxLength(200);
                settings.Property(s => s.ContactPhone).HasColumnName("Settings_ContactPhone").HasMaxLength(50);
                settings.Property(s => s.Address).HasColumnName("Settings_Address").HasMaxLength(500);
                settings.Property(s => s.LogoUrl).HasColumnName("Settings_LogoUrl").HasMaxLength(500);
                settings.Property(s => s.TaxCode).HasColumnName("Settings_TaxCode").HasMaxLength(20);
                // Store Finder coordinates (migrated from Shop entity, 2026-07-21)
                settings.Property(s => s.Latitude).HasColumnName("Settings_Latitude");
                settings.Property(s => s.Longitude).HasColumnName("Settings_Longitude");
                // Tenant Profile Page (2026-07-21): URL slug for /store/{slug} route.
                // Unique index — null allowed (tenants without public profile page).
                settings.Property(s => s.Slug).HasColumnName("Settings_Slug").HasMaxLength(100);
                settings.HasIndex(s => s.Slug).IsUnique();
                // Tenant Profile Page (2026-07-21): Social media links + brand story
                settings.Property(s => s.SocialLinksFb).HasColumnName("Settings_SocialLinksFb").HasMaxLength(500);
                settings.Property(s => s.SocialLinksTiktok).HasColumnName("Settings_SocialLinksTiktok").HasMaxLength(500);
                settings.Property(s => s.BrandStory).HasColumnName("Settings_BrandStory").HasMaxLength(500);
                // Theme Customization (2026-07-22): KhachLink UI theme (enum → int, default Classic=0)
                // NOTE: No HasDefaultValue() — EF Core treats default(0) as sentinel and skips UPDATE
                // when value equals default. The DB column has DEFAULT 0 from migration for inserts.
                settings.Property(s => s.Theme)
                    .HasColumnName("Settings_Theme")
                    .HasConversion<int>();
                // Sprint 7 — Commerce Mode override (enum → int, default Inherit=-1)
                // No HasDefaultValue — field initializer in TenantSettings sets Inherit.
                // HasDefaultValue would cause sentinel issue: Marketplace=0 (CLR default) → EF skips UPDATE.
                settings.Property(s => s.CommerceModeOverride)
                    .HasColumnName("Settings_CommerceModeOverride")
                    .HasConversion<int>()
                    .IsRequired();
                // TT 99 Phase 5a: B 09-DN Thuyết minh — tenant profile fields for Phần I.
                // Nullable — existing tenants get NULL → report shows "Chưa thiết lập".
                settings.Property(s => s.LegalForm).HasColumnName("Settings_LegalForm").HasMaxLength(100);
                settings.Property(s => s.BusinessField).HasColumnName("Settings_BusinessField").HasMaxLength(100);
                settings.Property(s => s.CharterCapital).HasColumnName("Settings_CharterCapital").HasColumnType("numeric(18,2)");
                // #93 — KhachLink style customization colors
                settings.Property(s => s.NavColor).HasColumnName("Settings_NavColor").HasMaxLength(20);
                settings.Property(s => s.HeaderColor).HasColumnName("Settings_HeaderColor").HasMaxLength(20);
                settings.Property(s => s.FooterColor).HasColumnName("Settings_FooterColor").HasMaxLength(20);
                // Crawl-to-Onboard Pipeline (2026-08-25, M3 resolved): Raw crawled phone number.
                // INTERNAL USE ONLY — NOT displayed on Pending profile (hide SĐT section per Luật 91/2025 Điều 16).
                // SysAdmin uses for owner identity verification during Claim approval.
                // After owner Verify: ContactPhone = owner-provided (consented); CrawledPhone should be cleared.
                settings.Property(s => s.CrawledPhone).HasColumnName("Settings_CrawledPhone").HasMaxLength(50);
            });

            // Crawl-to-Onboard Pipeline (2026-08-25): PotentialDuplicateOf flag.
            // Correction C1: Guid? (PK reference), NOT TenantId? value object — Single-Identity Pattern.
            // No FK constraint — just a nullable Guid reference (avoid cascade issues).
            // SysAdmin resolves duplicates (pick one to Verify, other → Inactive) before Verify can proceed.
            builder.Property(e => e.PotentialDuplicateOf)
                .HasColumnName("PotentialDuplicateOf");

            // Audit fields from BaseEntity
            builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.IsDeleted)
                .HasDefaultValue(false);

            // Ignore domain events (not persisted)
            builder.Ignore(e => e.DomainEvents);

            // W8: Map W2 conversion fields (was Ignore() in C2 fix — now persisted).
            // PredecessorTenantId / SuccessorTenantId: TenantId? value object → Guid? via TenantIdConverter.
            builder.Property(e => e.PredecessorTenantId)
                .HasColumnName("PredecessorTenantId")
                .HasConversion(new TenantIdConverter());
            builder.Property(e => e.SuccessorTenantId)
                .HasColumnName("SuccessorTenantId")
                .HasConversion(new TenantIdConverter());

            // ConvertedAt: nullable DateTime (audit trail for D9 conversion).
            builder.Property(e => e.ConvertedAt)
                .HasColumnName("ConvertedAt");

            // AccountingStandard: nullable enum → int? (HKD=null, DN=TT99/133/58).
            builder.Property(e => e.AccountingStandard)
                .HasColumnName("AccountingStandard")
                .HasConversion<int?>();

            // Type: nullable TenantType enum → int? (HKD=1, Enterprise_*=2/3/4).
            // C1 fix: classify tenants for W8 feature flag routing.
            builder.Property(e => e.Type)
                .HasColumnName("Type")
                .HasConversion<int?>();

            // Phase 1 (Multi-VPS Checkout): FK to ShopInstances table (nullable, no cascade delete).
            // Deleting a ShopInstance with assigned tenants is blocked by Restrict.
            builder.Property(e => e.ShopInstanceId)
                .HasColumnName("ShopInstanceId");
            builder.HasOne<ShopInstance>()
                .WithMany()
                .HasForeignKey(e => e.ShopInstanceId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(e => e.Id);
        }
    }
}

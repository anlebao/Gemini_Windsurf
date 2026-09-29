using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.MembershipAggregate;
using VanAn.CoreHub.Infrastructure.ValueConverters;
// QUAN TRỌNG: trỏ đúng TenantAggregate.Tenant (bảng "Tenants"), KHÔNG phải record obsolete VanAn.Shared.Domain.Tenant (bảng "Tenant")
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): EF Core configuration cho Member aggregate
    /// (Sổ đăng ký thành viên điện tử — SRS §17). PG-only (A3 — không sync SQLite).
    ///
    /// Data Integrity Contract (master_plan mục 6) — ĐIỂM QUAN TRỌNG NHẤT:
    /// - Polymorphic party = 2 FK thật + CHECK constraint exactly-one:
    ///   `MemberCustomerId Guid?` FK Customers.Id (Restrict) XOR `MemberTenantId Guid?` FK Tenants.Id (Restrict).
    ///   CẤM copy pattern Tenant.OwnerCustomerId (Guid ref không FK).
    /// - Unique (TenantId, MemberCustomerId) + (TenantId, MemberTenantId) — 1 party chỉ 1 membership/HTX.
    /// - BaseEntity.TenantId = htx_id (FK Tenants — Restrict).
    ///
    /// Auto-discovered via ApplyConfigurationsFromAssembly trong VanAnDbContext.OnModelCreating.
    /// </summary>
    public class MemberConfiguration : IEntityTypeConfiguration<Member>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<Member> builder)
        {
            builder.ToTable("Members",
                t => t.HasCheckConstraint("CK_Members_SingleParty",
                    // QUAN TRỌNG: tên cột phải QUOTED ("MemberCustomerId") — PostgreSQL lowercase hóa
                    // identifier unquoted → 42703 "column membercustomerid does not exist" (prod incident 2026-09-29).
                    // EF Core truyền nguyên chuỗi CHECK; Npgsql đặt tên cột quoted nên CHECK phải quote khớp.
                    "(\"MemberCustomerId\" IS NULL) <> (\"MemberTenantId\" IS NULL)"));

            _ = builder.HasKey(e => e.Id);

            // ── Party (polymorphic — exactly-one, enforced by CHECK) ──
            _ = builder.Property(e => e.MemberCustomerId);
            // TenantId? VO → TEXT (pattern Tenant.PredecessorTenantId — global convention không cover nullable)
            _ = builder.Property(e => e.MemberTenantId)
                .HasConversion(new TenantIdConverter());

            // ── Registry ──
            _ = builder.Property(e => e.MembershipType).HasConversion<int>().IsRequired();
            _ = builder.Property(e => e.MemberNumber).IsRequired().HasMaxLength(50);
            _ = builder.Property(e => e.Status).HasConversion<int>().IsRequired();
            _ = builder.Property(e => e.JoinedAt).IsRequired();
            _ = builder.Property(e => e.ApprovedAt);
            _ = builder.Property(e => e.EffectiveAt);
            _ = builder.Property(e => e.TerminatedAt);
            _ = builder.Property(e => e.StatusReason).HasMaxLength(1000);

            // ── Audit fields từ BaseEntity ──
            _ = builder.Property(e => e.CreatedAt);
            _ = builder.Property(e => e.UpdatedAt);

            // ── FKs (Data Integrity Contract — FK thật + Restrict) ──
            // htx_id qua BaseEntity.TenantId (TenantIdConverter global) — Restrict
            builder.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            // Cá nhân (nullable)
            builder.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(e => e.MemberCustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            // HKD/DN (nullable)
            builder.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(e => e.MemberTenantId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Indexes ──
            _ = builder.HasIndex(e => e.TenantId).HasDatabaseName("IX_Members_TenantId");
            _ = builder.HasIndex(e => e.MemberNumber).IsUnique().HasDatabaseName("UX_Members_MemberNumber");
            _ = builder.HasIndex(e => new { e.TenantId, e.MemberCustomerId })
                .IsUnique().HasDatabaseName("UX_Members_HtxCustomer");
            _ = builder.HasIndex(e => new { e.TenantId, e.MemberTenantId })
                .IsUnique().HasDatabaseName("UX_Members_HtxTenant");
        }
    }
}

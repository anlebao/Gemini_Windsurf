using System.Security.Claims;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.UI.Platform.Models;

namespace VanAn.ShopERP.Services;

/// <summary>
/// Single Source of Truth for the ShopERP sidebar menu.
/// All layouts (MainLayout/NavMenu, AdminLayout, AccountingLayout, EInvoiceLayout)
/// call <see cref="BuildAsync"/> so the SAME role always sees the SAME menu,
/// regardless of which page/layout they navigate to.
/// Merged from the former NavMenu.razor + AdminLayout.razor definitions, plus
/// missing items (Tài chính for Owner/SystemAdmin, Claims/Crawl for SystemAdmin, ...).
/// </summary>
public interface IShopErpMenuService
{
    Task<List<NavigationItem>> BuildAsync(ClaimsPrincipal user);
}

/// <inheritdoc cref="IShopErpMenuService" />
public sealed class ShopErpMenuService : IShopErpMenuService
{
    private readonly ITenantProvider _tenantProvider;
    private readonly IVasFeatureFlagService _featureFlagService;

    public ShopErpMenuService(ITenantProvider tenantProvider, IVasFeatureFlagService featureFlagService)
    {
        _tenantProvider = tenantProvider;
        _featureFlagService = featureFlagService;
    }

    public async Task<List<NavigationItem>> BuildAsync(ClaimsPrincipal user)
    {
        var items = new List<NavigationItem>();
        if (user.Identity?.IsAuthenticated != true)
        {
            return items;
        }

        var isMasterchef = user.IsInRole("Masterchef");
        var isStaff = user.IsInRole("Staff");
        var isStoreKeeper = user.IsInRole("StoreKeeper");
        var isOwner = user.IsInRole("Owner");
        var isGuard = user.IsInRole("Guard");
        var isSystemAdmin = user.IsInRole("SystemAdmin");
        // Issue #103: When SystemAdmin is impersonating, treat as Owner for menu visibility.
        // Hide platform-admin menus (Hệ thống, CRM Global, Hướng dẫn) and show Owner menus instead.
        if (user.FindFirst("impersonating")?.Value == "true")
        {
            isSystemAdmin = false;
            isOwner = true;
        }

        // Home / Sitemap
        items.Add(new() { Title = "Home", Icon = "house-door-fill", Url = isMasterchef ? "/kitchen" : "/sitemap" });

        // Masterchef: Kitchen ONLY
        if (isMasterchef)
        {
            items.Add(new() { Title = "Bếp", Icon = "fire", Url = "/kitchen" });
        }

        // Vận hành: Staff, StoreKeeper, Owner
        if (isStaff || isStoreKeeper || isOwner)
        {
            items.Add(new()
            {
                Title = "Vận hành", Icon = "bag-fill",
                Children = new()
                {
                    new() { Title = "POS", Icon = "bag", Url = "/pos" },
                    new() { Title = "Bếp", Icon = "fire", Url = "/kitchen" },
                    new() { Title = "Đơn hàng", Icon = "list-check", Url = "/orders" },
                }
            });
        }

        // Sản phẩm: Owner
        if (isOwner)
        {
            items.Add(new()
            {
                Title = "Sản phẩm", Icon = "box-seam",
                Children = new()
                {
                    new() { Title = "Sản phẩm", Icon = "box-seam", Url = "/products" },
                }
            });
        }

        // Kế Toán: Owner
        if (isOwner)
        {
            var accountingChildren = new List<NavigationItem>
            {
                new() { Title = "Kế Toán", Icon = "journal-text", Url = "/accounting" },
                new() { Title = "Lịch Sử Giao Dịch", Icon = "clock-history", Url = "/accounting/history" },
                new() { Title = "Số Dư Tài Khoản", Icon = "currency-exchange", Url = "/accounting/balance" },
                new() { Title = "Đóng Kỳ Kế Toán", Icon = "lock-fill", Url = "/accounting/period-closing" },
            };

            // Preserve AccountingLayout behavior: HKD tenants see "Sổ HKD", Enterprise tenants see "Báo Cáo Tài Chính"
            if (_tenantProvider.HasTenant)
            {
                try
                {
                    var isEnterprise = await _featureFlagService.CanAccessVasReportsAsync(new TenantId(_tenantProvider.TenantId));
                    if (isEnterprise)
                    {
                        accountingChildren.Add(new() { Title = "Báo Cáo Tài Chính", Icon = "bar-chart", Url = "/accounting/financial-reports" });
                    }
                    else
                    {
                        accountingChildren.Add(new() { Title = "Sổ HKD (TT 152)", Icon = "book", Url = "/accounting/hkd-books" });
                    }
                }
                catch
                {
                    // Safe default: HKD is the common case
                    accountingChildren.Add(new() { Title = "Sổ HKD (TT 152)", Icon = "book", Url = "/accounting/hkd-books" });
                }
            }

            items.Add(new() { Title = "Kế Toán", Icon = "journal-text", Children = accountingChildren });
        }

        // Hóa Đơn Điện Tử: Owner, StoreKeeper, SystemAdmin
        if (isOwner || isStoreKeeper || isSystemAdmin)
        {
            var einvoiceChildren = new List<NavigationItem>
            {
                new() { Title = "Dashboard", Icon = "file-earmark-text", Url = "/einvoice" },
                new() { Title = "Quản Lý Hóa Đơn", Icon = "receipt", Url = "/einvoice/invoices" },
                new() { Title = "Quản Lý Cảnh Báo", Icon = "bell", Url = "/einvoice/alerts" },
                new() { Title = "Health Monitoring", Icon = "heart-pulse", Url = "/einvoice/health" },
            };

            // Provider Config: Owner + SystemAdmin only
            if (isOwner || isSystemAdmin)
            {
                einvoiceChildren.Add(new() { Title = "Quản Lý Provider", Icon = "gear-fill", Url = "/einvoice/providers" });
                einvoiceChildren.Add(new() { Title = "Cấu Hình Provider", Icon = "sliders", Url = "/einvoice/configuration" });
            }

            items.Add(new()
            {
                Title = "Hóa Đơn Điện Tử", Icon = "file-earmark-text",
                Children = einvoiceChildren
            });
        }

        // CRM & Loyalty (per-tenant): Owner
        if (isOwner)
        {
            items.Add(new()
            {
                Title = "CRM & Loyalty", Icon = "award",
                Children = new()
                {
                    new() { Title = "Khách hàng (CRM)", Icon = "people-fill", Url = "/admin/customers" },
                    new() { Title = "Chiến dịch khuyến mãi", Icon = "megaphone-fill", Url = "/admin/promo-campaigns" },
                    new() { Title = "Quản lý Nhiệm vụ", Icon = "trophy-fill", Url = "/admin/missions" },
                    new() { Title = "Đổi điểm (Catalog)", Icon = "gift-fill", Url = "/admin/redemption-catalog" },
                    new() { Title = "Lịch sử đổi điểm", Icon = "clock-history", Url = "/admin/redemption-history" },
                    new() { Title = "Quản lý cộng tác viên", Icon = "people-fill", Url = "/community/owner-panel" },
                    // Realtime Platform P5 (2026-09-18): hộp thư tin nhắn chủ shop (Shop chat)
                    new() { Title = "Hộp thư tin nhắn", Icon = "chat-dots-fill", Url = "/community/messages" },
                    new() { Title = "Cấu hình tính năng", Icon = "toggles", Url = "/settings/shop-features" },
                    new() { Title = "Thống kê điểm thưởng", Icon = "bar-chart-fill", Url = "/loyalty/dashboard" },
                }
            });
        }

        // Quản trị (HR): Owner
        if (isOwner)
        {
            items.Add(new()
            {
                Title = "Quản trị", Icon = "shield-lock",
                Children = new()
                {
                    new() { Title = "Quản lý người dùng", Icon = "people-fill", Url = "/admin/users" },
                    new() { Title = "Nhóm quyền", Icon = "person-lock", Url = "/admin/permission-groups" },
                }
            });
        }

        // Tài chính: Owner + SystemAdmin (was Owner-only in AdminLayout; SystemAdmin had no link before)
        if (isOwner || isSystemAdmin)
        {
            items.Add(new()
            {
                Title = "Tài chính", Icon = "graph-up-arrow",
                Children = new()
                {
                    new() { Title = "Thông tin tài chính", Icon = "graph-up-arrow", Url = "/financial" },
                    new() { Title = "Hồ sơ doanh nghiệp", Icon = "building-fill-gear", Url = "/admin/business-profile" },
                }
            });
        }

        // Hệ thống: SystemAdmin only
        if (isSystemAdmin)
        {
            items.Add(new()
            {
                Title = "Hệ thống", Icon = "server",
                Children = new()
                {
                    new() { Title = "Tenants", Icon = "building", Url = "/admin/tenants" },
                    new() { Title = "Hàng đợi Claim", Icon = "shield-check", Url = "/admin/claims" },
                    new() { Title = "Kích hoạt Crawl", Icon = "download", Url = "/admin/crawl-trigger" },
                    new() { Title = "Tenant Registrations", Icon = "person-plus", Url = "/admin/tenant-registrations" },
                    new() { Title = "ShopERP Instances", Icon = "hdd-network", Url = "/admin/shop-instances" },
                    new() { Title = "KhachLink Instances", Icon = "grid", Url = "/admin/khachlink-instances" },
                    new() { Title = "Tenant Domains", Icon = "globe", Url = "/admin/domains" },
                    new() { Title = "Người dùng", Icon = "people", Url = "/admin/users" },
                    new() { Title = "Nhóm quyền", Icon = "person-lock", Url = "/admin/permission-groups" },
                    new() { Title = "Nhật Ký Audit", Icon = "shield-check", Url = "/admin/audit-trail" },
                    new() { Title = "Background Services", Icon = "arrow-clockwise", Url = "/admin/background-services" },
                }
            });
        }

        // CRM & Loyalty (Cross-tenant): SystemAdmin only
        if (isSystemAdmin)
        {
            items.Add(new()
            {
                Title = "CRM & Loyalty (Global)", Icon = "award",
                Children = new()
                {
                    new() { Title = "Cross-Tenant Overview", Icon = "globe", Url = "/admin/customers-global" },
                    new() { Title = "Khách hàng (CRM)", Icon = "people-fill", Url = "/admin/customers" },
                    new() { Title = "Chiến dịch khuyến mãi", Icon = "megaphone-fill", Url = "/admin/promo-campaigns" },
                    new() { Title = "Push Campaigns", Icon = "bell-fill", Url = "/admin/push-campaigns" },
                    new() { Title = "Khuyến mãi (Social)", Icon = "megaphone", Url = "/admin/campaigns" },
                    new() { Title = "Quản lý Nhiệm vụ", Icon = "trophy-fill", Url = "/admin/missions" },
                    new() { Title = "Đổi điểm (Catalog)", Icon = "gift", Url = "/admin/redemption-catalog" },
                    new() { Title = "Catalog Global", Icon = "globe2", Url = "/admin/global-redemption-catalog" },
                    new() { Title = "Lịch sử đổi điểm", Icon = "clock-history", Url = "/admin/redemption-history" },
                    new() { Title = "Loyalty Alliance Config", Icon = "award-fill", Url = "/admin/loyalty-config" },
                    new() { Title = "Thống kê điểm thưởng", Icon = "bar-chart-fill", Url = "/loyalty/dashboard" },
                }
            });
        }

        // Cộng tác viên: SystemAdmin only
        if (isSystemAdmin)
        {
            items.Add(new()
            {
                Title = "Cộng tác viên", Icon = "people-fill",
                Children = new()
                {
                    new() { Title = "Quản lý CTV", Icon = "people-fill", Url = "/admin/community/admin-panel" },
                    new() { Title = "Fraud Review", Icon = "shield-exclamation", Url = "/admin/community/fraud-flags" },
                    new() { Title = "Fraud Stats", Icon = "graph-up-arrow", Url = "/admin/community/fraud-stats" },
                    new() { Title = "Device Registrations", Icon = "phone-vibrate", Url = "/admin/device-registrations" },
                    new() { Title = "Cấu hình tính năng", Icon = "toggles", Url = "/settings/shop-features" },
                    new() { Title = "Xác minh SMS", Icon = "phone", Url = "/admin/collaborator-verification" },
                    new() { Title = "Quỹ Cộng Đồng", Icon = "piggy-bank", Url = "/admin/community-fund" },
                    new() { Title = "Lịch sử Settlement", Icon = "cash-coin", Url = "/admin/settlements" },
                }
            });
        }

        // Thương mại: SystemAdmin only
        if (isSystemAdmin)
        {
            items.Add(new()
            {
                Title = "Thương mại", Icon = "shop",
                Children = new()
                {
                    new() { Title = "Commerce Mode", Icon = "toggle2-on", Url = "/admin/commerce-mode" },
                    new() { Title = "Referral Configs", Icon = "diagram-3", Url = "/admin/product-referral-configs" },
                    new() { Title = "Giá Vốn SP", Icon = "tag", Url = "/admin/product-cost-prices" },
                    new() { Title = "VALCN v2.0 Features", Icon = "toggles", Url = "/admin/valcn-features" },
                    new() { Title = "Sản phẩm nổi bật", Icon = "star", Url = "/admin/featured-products" },
                    new() { Title = "Reseller Kế toán", Icon = "receipt", Url = "/admin/reseller-accounting-reconciliation" },
                }
            });
        }

        // Infrastructure: SystemAdmin only
        if (isSystemAdmin)
        {
            items.Add(new()
            {
                Title = "Infrastructure", Icon = "hdd-stack",
                Children = new()
                {
                    new() { Title = "Network Dashboard", Icon = "graph-up", Url = "/admin/network-dashboard" },
                    new() { Title = "Lưu trữ ảnh R2", Icon = "cloud-arrow-down", Url = "/admin/r2-storage" },
                    new() { Title = "Cài đặt OCR", Icon = "camera", Url = "/admin/ocr-settings" },
                    new() { Title = "Cấu hình KhachLink", Icon = "sliders2-vertical", Url = "/admin/khachlink-home-settings" },
                }
            });
        }

        // Hướng dẫn: SystemAdmin only
        if (isSystemAdmin)
        {
            items.Add(new()
            {
                Title = "Hướng dẫn", Icon = "book",
                Children = new()
                {
                    new() { Title = "KhachLink Multi-Profile", Icon = "grid", Url = "guides/KhachLink_Multi_Profile_Usage_Guide.html" },
                    new() { Title = "CRM & Loyalty", Icon = "award", Url = "guides/CRM_Loyalty_Guide.html" },
                    new() { Title = "VALCN v2.0 Platform", Icon = "toggles", Url = "guides/VALCN_V2_Platform_User_Guide.html" },
                    new() { Title = "Community Commerce", Icon = "people-fill", Url = "guides/community-commerce/README.html" },
                }
            });
        }

        // Guard: Guard role only
        if (isGuard)
        {
            items.Add(new()
            {
                Title = "Guard", Icon = "shield-check",
                Children = new()
                {
                    new() { Title = "Quét QR Guard", Icon = "qr-code-scan", Url = "/guard/scan" },
                }
            });
        }

        // Logout: visible to all authenticated users
        items.Add(new() { Title = "Đăng xuất", Icon = "box-arrow-right", Url = "/Logout" });

        return items;
    }
}

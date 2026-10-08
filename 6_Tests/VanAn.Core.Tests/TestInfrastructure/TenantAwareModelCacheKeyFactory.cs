using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.CoreHub.Tests.TestInfrastructure
{
    /// <summary>
    /// Model cache key theo (context type + tenant + hasProvider) — cho phép SHARE EF model
    /// giữa các test CÙNG tenant thay vì rebuild model mỗi test.
    ///
    /// Nối tiếp pattern `1d211a3c` (fix(tests): eliminate flaky parallel test failures via
    /// EF Core model cache isolation, 2026-08-02): fix cũ tạo internal ServiceProvider riêng
    /// per test để cô lập model cache, nhưng model đã phình to (~100 entity — Booking +19)
    /// → rebuild model ~15-22s/test → fast test gate 2156 tests kẹt hàng giờ.
    ///
    /// Key gồm CurrentTenantId: filter tenant capture context instance tại model-build
    /// (VanAnDbContext.ApplyMultiTenancyFilters — Expression.Constant(capturedContext)),
    /// nhưng giá trị đọc từ provider tại QUERY TIME. Cùng tenant → cùng giá trị filter
    /// → chia sẻ model an toàn; khác tenant → model riêng (không nhiễu cross-tenant).
    /// </summary>
    public sealed class TenantAwareModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
        {
            if (context is VanAnDbContext vanAn)
            {
                return (context.GetType(), vanAn.CurrentTenantId, designTime);
            }

            return (context.GetType(), designTime);
        }
    }
}

using NetArchTest.Rules;
using Xunit;
using VanAn.CoreHub.Infrastructure; // Điều chỉnh namespace cho đúng với VanAnDbContext của bạn

namespace VanAn.Tests.Architecture
{
    public class ArchitectureIntegrityTests
    {
        [Fact]
        public void CoreHub_ShouldNot_Use_InMemoryDatabase()
        {
            // Bắt hệ thống quét toàn bộ project CoreHub
            var result = Types.InAssembly(typeof(VanAn.CoreHub.Infrastructure.VanAnDbContext).Assembly)
                .ShouldNot()
                .HaveDependencyOn("Microsoft.EntityFrameworkCore.InMemory")
                .GetResult();

            Assert.True(result.IsSuccessful, 
                "CẢNH BÁO ĐỎ: Phát hiện sử dụng In-Memory Database trong code Production. Yêu cầu tuân thủ Hiến pháp Vạn An và revert ngay lập tức!");
        }
    }
}

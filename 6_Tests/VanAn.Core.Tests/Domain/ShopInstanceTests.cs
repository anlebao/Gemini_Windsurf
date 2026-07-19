using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Domain
{
    /// <summary>
    /// Phase 1 (Multi-VPS Checkout): Unit tests for ShopInstance entity.
    /// Verifies factory validation, health updates, and lifecycle methods.
    /// </summary>
    public class ShopInstanceTests
    {
        [Fact]
        public void Create_SetsProperties_Correctly()
        {
            var instance = new ShopInstance("http://shoperp:5003", "VPS-1 HCM");
            Assert.Equal("http://shoperp:5003", instance.BaseUrl);
            Assert.Equal("VPS-1 HCM", instance.Label);
            Assert.Equal(50, instance.MaxTenants);
            Assert.True(instance.IsActive);
            Assert.Equal("Unknown", instance.HealthStatus);
            Assert.Null(instance.HealthCheckUrl);
            Assert.Null(instance.LastHealthCheck);
        }

        [Fact]
        public void Create_WithEmptyBaseUrl_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new ShopInstance("", "VPS-1"));
        }

        [Fact]
        public void Create_WithEmptyLabel_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new ShopInstance("http://shoperp:5003", ""));
        }

        [Fact]
        public void Create_WithNegativeMaxTenants_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new ShopInstance("http://shoperp:5003", "VPS-1", -1));
        }

        [Fact]
        public void Create_WithCustomMaxTenants_StoresValue()
        {
            var instance = new ShopInstance("http://shoperp:5003", "VPS-1", 100, "/health");
            Assert.Equal(100, instance.MaxTenants);
            Assert.Equal("/health", instance.HealthCheckUrl);
        }

        [Fact]
        public void UpdateHealth_SetsStatusAndTimestamp()
        {
            var instance = new ShopInstance("http://shoperp:5003", "VPS-1");
            var checkedAt = new DateTime(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc);

            instance.UpdateHealth("Healthy", checkedAt);

            Assert.Equal("Healthy", instance.HealthStatus);
            Assert.Equal(checkedAt, instance.LastHealthCheck);
        }

        [Fact]
        public void UpdateHealth_WithEmptyStatus_ThrowsArgumentException()
        {
            var instance = new ShopInstance("http://shoperp:5003", "VPS-1");
            Assert.Throws<ArgumentException>(() => instance.UpdateHealth(""));
        }

        [Fact]
        public void UpdateHealth_WithoutTimestamp_DefaultsToUtcNow()
        {
            var instance = new ShopInstance("http://shoperp:5003", "VPS-1");
            var before = DateTime.UtcNow;

            instance.UpdateHealth("Degraded");

            var after = DateTime.UtcNow;
            Assert.Equal("Degraded", instance.HealthStatus);
            Assert.InRange(instance.LastHealthCheck!.Value, before, after);
        }

        [Fact]
        public void Deactivate_SetsIsActiveFalse()
        {
            var instance = new ShopInstance("http://shoperp:5003", "VPS-1");
            Assert.True(instance.IsActive);

            instance.Deactivate();

            Assert.False(instance.IsActive);
        }

        [Fact]
        public void Activate_SetsIsActiveTrue()
        {
            var instance = new ShopInstance("http://shoperp:5003", "VPS-1");
            instance.Deactivate();
            Assert.False(instance.IsActive);

            instance.Activate();

            Assert.True(instance.IsActive);
        }

        [Fact]
        public void UpdateLabel_ThrowsForEmptyLabel()
        {
            var instance = new ShopInstance("http://shoperp:5003", "VPS-1");
            Assert.Throws<ArgumentException>(() => instance.UpdateLabel(""));
        }

        [Fact]
        public void UpdateLabel_SetsNewLabel()
        {
            var instance = new ShopInstance("http://shoperp:5003", "VPS-1");
            instance.UpdateLabel("VPS-2 HN");
            Assert.Equal("VPS-2 HN", instance.Label);
        }

        [Fact]
        public void UpdateMaxTenants_ThrowsForNegative()
        {
            var instance = new ShopInstance("http://shoperp:5003", "VPS-1");
            Assert.Throws<ArgumentException>(() => instance.UpdateMaxTenants(-5));
        }

        [Fact]
        public void UpdateMaxTenants_SetsNewValue()
        {
            var instance = new ShopInstance("http://shoperp:5003", "VPS-1", 50);
            instance.UpdateMaxTenants(200);
            Assert.Equal(200, instance.MaxTenants);
        }
    }
}

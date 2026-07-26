using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Community
{
    /// <summary>
    /// DeviceRegistration entity tests (Community Commerce Sprint 0 v1.2).
    /// Cases 23-26: creation, Touch, Deactivate, Verify.
    /// </summary>
    public class DeviceRegistrationTests
    {
        private static DeviceRegistration CreateDevice()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            return new DeviceRegistration(
                tenantId,
                Guid.NewGuid(),
                "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                "{}",
                "Mozilla/5.0",
                "Web",
                "127.0.0.1");
        }

        [Fact(DisplayName = "23: DeviceRegistration_Create_ValidFields")]
        public void DeviceRegistration_Create_ValidFields()
        {
            var device = CreateDevice();
            Assert.Equal(64, device.DeviceToken.Length);
            Assert.Equal(64, device.FingerprintHash.Length);
            Assert.True(device.IsActive);
            Assert.False(device.IsVerified);
            Assert.NotEqual(DateTime.MinValue, device.FirstSeenAt);
            Assert.NotEqual(DateTime.MinValue, device.LastSeenAt);
        }

        [Fact(DisplayName = "24: DeviceRegistration_Touch_UpdatesLastSeenAndIp")]
        public void DeviceRegistration_Touch_UpdatesLastSeenAndIp()
        {
            var device = CreateDevice();
            var newLastSeen = DateTime.UtcNow.AddMinutes(5);
            var newIp = "192.168.1.1";

            device.Touch(newLastSeen, newIp);

            Assert.Equal(newLastSeen, device.LastSeenAt);
            Assert.Equal(newIp, device.IpAddress);
        }

        [Fact(DisplayName = "25: DeviceRegistration_Deactivate_SetsIsActiveFalse")]
        public void DeviceRegistration_Deactivate_SetsIsActiveFalse()
        {
            var device = CreateDevice();
            device.Deactivate();
            Assert.False(device.IsActive);
        }

        [Fact(DisplayName = "26: DeviceRegistration_Verify_SetsIsVerifiedTrue")]
        public void DeviceRegistration_Verify_SetsIsVerifiedTrue()
        {
            var device = CreateDevice();
            device.Verify();
            Assert.True(device.IsVerified);
        }
    }
}

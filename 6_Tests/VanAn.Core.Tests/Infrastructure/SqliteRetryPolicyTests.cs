using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using VanAn.ShopERP.Infrastructure;
using Xunit;
using Moq;

namespace VanAn.Core.Tests.Infrastructure
{
    /// <summary>
    /// Tests for SqliteRetryPolicy - SQLite concurrency handling
    /// </summary>
    public class SqliteRetryPolicyTests
    {
        private readonly Mock<ILogger> _loggerMock;

        public SqliteRetryPolicyTests()
        {
            _loggerMock = new Mock<ILogger>();
        }

        [Fact]
        public async Task ExecuteWithRetryAsync_ShouldSucceed_WhenNoException()
        {
            // Arrange
            int callCount = 0;
            Task<int> operation()
            {
                callCount++;
                return Task.FromResult(callCount);
            }

            // Act
            int result = await SqliteRetryPolicy.ExecuteWithRetryAsync(operation, _loggerMock.Object);

            // Assert
            Assert.Equal(1, callCount);
            Assert.Equal(1, result);
        }

        [Fact]
        public async Task ExecuteWithRetryAsync_ShouldRetry_WhenSQLiteBusyException()
        {
            // Arrange
            int callCount = 0;
            Task<int> operation()
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new SqliteException("Database is busy", 5); // SqliteErrorCode.Busy = 5
                }
                return Task.FromResult(callCount);
            }

            // Act
            int result = await SqliteRetryPolicy.ExecuteWithRetryAsync(operation, _loggerMock.Object);

            // Assert
            Assert.Equal(2, callCount);
            Assert.Equal(2, result);
        }

        [Fact]
        public async Task ExecuteWithRetryAsync_ShouldRetry_WhenSQLiteLockedException()
        {
            // Arrange
            int callCount = 0;
            Task<int> operation()
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new SqliteException("Database is locked", 6); // SqliteErrorCode.Locked = 6
                }
                return Task.FromResult(callCount);
            }

            // Act
            int result = await SqliteRetryPolicy.ExecuteWithRetryAsync(operation, _loggerMock.Object);

            // Assert
            Assert.Equal(2, callCount);
            Assert.Equal(2, result);
        }

        [Fact]
        public async Task ExecuteWithRetryAsync_ShouldFail_WhenMaxRetriesExceeded()
        {
            // Arrange
            static Task<int> operation()
            {
                throw new SqliteException("Database is busy", 5); // SqliteErrorCode.Busy = 5
            }

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                SqliteRetryPolicy.ExecuteWithRetryAsync(operation, _loggerMock.Object));
        }

        [Fact]
        public async Task ExecuteWithRetryAsync_ShouldNotRetry_WhenNonRetryableException()
        {
            // Arrange
            int callCount = 0;
            Task<int> operation()
            {
                callCount++;
                return callCount == 1 ? throw new InvalidOperationException("Non-retryable error") : Task.FromResult(callCount);
            }

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                SqliteRetryPolicy.ExecuteWithRetryAsync(operation, _loggerMock.Object));

            Assert.Equal(1, callCount); // Should not have retried
        }

        [Fact]
        public async Task ExecuteWithRetryAsync_VoidMethod_ShouldWork()
        {
            // Arrange
            int callCount = 0;
            Task operation()
            {
                callCount++;
                return Task.CompletedTask;
            }

            // Act
            await SqliteRetryPolicy.ExecuteWithRetryAsync(operation, _loggerMock.Object);

            // Assert
            Assert.Equal(1, callCount);
        }
    }
}

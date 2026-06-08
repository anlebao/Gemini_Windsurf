using System.Diagnostics;
using Xunit.Sdk;

namespace VanAn.Tests.Common
{
    /// <summary>
    /// Helper class for async test assertions with polling patterns.
    /// Replaces flaky Task.Delay-based waits with condition-based polling.
    /// </summary>
    public static class AsyncAssert
    {
        /// <summary>
        /// Waits for a condition to become true within a specified timeout.
        /// Polls at regular intervals until the condition is met or timeout expires.
        /// </summary>
        /// <param name="condition">Async condition to check</param>
        /// <param name="timeout">Maximum time to wait</param>
        /// <param name="pollInterval">Interval between condition checks</param>
        /// <param name="message">Message to include in timeout exception</param>
        /// <exception cref="TimeoutException">Thrown when condition is not met within timeout</exception>
        public static async Task WaitForConditionAsync(
            Func<Task<bool>> condition,
            TimeSpan timeout,
            TimeSpan pollInterval,
            string message)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                if (await condition())
                    return;
                await Task.Delay(pollInterval);
            }
            throw new TimeoutException(message);
        }

        /// <summary>
        /// Waits for a condition to become true within a specified timeout.
        /// Synchronous version for simple conditions.
        /// </summary>
        /// <param name="condition">Condition to check</param>
        /// <param name="timeout">Maximum time to wait</param>
        /// <param name="pollInterval">Interval between condition checks</param>
        /// <param name="message">Message to include in timeout exception</param>
        /// <exception cref="TimeoutException">Thrown when condition is not met within timeout</exception>
        public static async Task WaitForConditionAsync(
            Func<bool> condition,
            TimeSpan timeout,
            TimeSpan pollInterval,
            string message)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                if (condition())
                    return;
                await Task.Delay(pollInterval);
            }
            throw new TimeoutException(message);
        }

        /// <summary>
        /// Waits for a queue or processing system to complete processing items.
        /// Specifically designed for OrderQueueService and similar queue-based systems.
        /// </summary>
        /// <typeparam name="TMetrics">Type of the metrics object</typeparam>
        /// <param name="getMetrics">Function to retrieve current metrics</param>
        /// <param name="minProcessedCount">Minimum number of items that should be processed</param>
        /// <param name="timeout">Maximum time to wait</param>
        /// <param name="pollInterval">Interval between metric checks</param>
        /// <param name="message">Message to include in timeout exception</param>
        public static async Task WaitForProcessingAsync<TMetrics>(
            Func<Task<TMetrics>> getMetrics,
            Func<TMetrics, int> getProcessedCount,
            int minProcessedCount,
            TimeSpan timeout,
            TimeSpan pollInterval,
            string message)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                var metrics = await getMetrics();
                var processed = getProcessedCount(metrics);
                if (processed >= minProcessedCount)
                    return;
                await Task.Delay(pollInterval);
            }
            throw new TimeoutException(message);
        }

        /// <summary>
        /// Default timeout for polling operations (30 seconds).
        /// </summary>
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Default poll interval for polling operations (500ms).
        /// </summary>
        public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(500);
    }
}

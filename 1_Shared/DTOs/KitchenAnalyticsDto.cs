using System;
using System.Collections.Generic;

namespace VanAn.Shared.DTOs
{
    /// <summary>
    /// Kitchen analytics data transfer object
    /// </summary>
    public class KitchenAnalyticsDto
    {
        public Guid ShopId { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public int TotalOrders { get; set; }
        public int CompletedOrders { get; set; }
        public int PendingOrders { get; set; }
        public double AveragePreparationTime { get; set; }
        public List<KitchenPerformanceDto> Performance { get; set; } = new();
    }

    /// <summary>
    /// Kitchen performance metrics
    /// </summary>
    public class KitchenPerformanceDto
    {
        public DateTime Date { get; set; }
        public int OrdersProcessed { get; set; }
        public double AverageTime { get; set; }
        public int PeakHourOrders { get; set; }
    }
}

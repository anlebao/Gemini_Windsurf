namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Wave 3: Excel export service for operational reports.
    /// Produces .xlsx files using EPPlus (NonCommercial license for MVP).
    /// </summary>
    public interface IExcelExportService
    {
        /// <summary>
        /// Export revenue report for a tenant within a date range.
        /// </summary>
        Task<byte[]> ExportRevenueAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken cancellationToken = default);

        /// <summary>
        /// Export current inventory report for a tenant.
        /// </summary>
        Task<byte[]> ExportInventoryAsync(Guid tenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Export customer report for a tenant within a date range.
        /// </summary>
        Task<byte[]> ExportCustomerAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    }
}

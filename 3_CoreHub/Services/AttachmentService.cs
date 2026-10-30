using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// SRS v1.2 W2/C1 (G-H3, M10): đính kèm chứng từ — lưu/đọc link file tới entity kế toán.
    /// Upload endpoint (R2/Cloudinary) Phase sau — trước mắt field + API nội bộ (card §2.5).
    /// </summary>
    public interface IAttachmentService
    {
        Task<AttachmentEntity> AddAsync(
            TenantId tenantId, string entityType, Guid entityId, string fileUrl,
            string? fileName = null, string? mimeType = null, string? uploadedBy = null,
            CancellationToken cancellationToken = default);

        Task<List<AttachmentEntity>> GetForAsync(
            TenantId tenantId, string entityType, Guid entityId, CancellationToken cancellationToken = default);
    }

    public sealed class AttachmentService(IAccountingDbContext dbContext, ILogger<AttachmentService> logger) : IAttachmentService
    {
        private readonly IAccountingDbContext _dbContext = dbContext;
        private readonly ILogger<AttachmentService> _logger = logger;

        /// <inheritdoc />
        public async Task<AttachmentEntity> AddAsync(
            TenantId tenantId, string entityType, Guid entityId, string fileUrl,
            string? fileName = null, string? mimeType = null, string? uploadedBy = null,
            CancellationToken cancellationToken = default)
        {
            var attachment = new AttachmentEntity(tenantId.Value, entityType, entityId, fileUrl, fileName, mimeType, uploadedBy);
            _ = await _dbContext.Attachments.AddAsync(attachment, cancellationToken);
            _ = await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Attachment {AttachmentId} added for {EntityType}/{EntityId} (tenant {TenantId})",
                attachment.Id, entityType, entityId, tenantId.Value);
            return attachment;
        }

        /// <inheritdoc />
        public async Task<List<AttachmentEntity>> GetForAsync(
            TenantId tenantId, string entityType, Guid entityId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Attachments
                .AsNoTracking()
                .Where(a => a.TenantId == tenantId.Value && a.EntityType == entityType && a.EntityId == entityId)
                .OrderBy(a => a.CreatedAt)
                .ToListAsync(cancellationToken);
        }
    }
}

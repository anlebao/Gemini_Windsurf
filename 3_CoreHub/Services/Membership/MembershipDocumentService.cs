using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.MembershipAggregate;

namespace VanAn.CoreHub.Services.Membership
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): Tài liệu đính kèm hồ sơ (SRS §17.2).
    /// Append-only evidence — chỉ thêm, không Update/Delete.
    /// </summary>
    public class MembershipDocumentService(
        IVanAnDbContext dbContext,
        ILogger<MembershipDocumentService> logger) : IMembershipDocumentService
    {
        public async Task<Guid> AttachAsync(
            Guid applicationId,
            Guid applicantCustomerId,
            MembershipDocumentType documentType,
            string documentVersion,
            string storageReference,
            string? hash = null,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(storageReference);
            if (applicantCustomerId == Guid.Empty)
                throw new ArgumentException("ApplicantCustomerId cannot be empty.", nameof(applicantCustomerId));

            var application = await dbContext.MembershipApplications
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.Id == applicationId, ct)
                ?? throw new KeyNotFoundException($"Membership application {applicationId} not found.");

            // IDOR guard: chỉ applicant sở hữu hồ sơ mới đính kèm được
            if (application.ApplicantCustomerId != applicantCustomerId)
                throw new UnauthorizedAccessException("Applicant does not own this application.");

            var document = MembershipDocument.Create(
                application.TenantId,
                applicationId,
                documentType,
                documentVersion,
                storageReference,
                hash);

            dbContext.MembershipDocuments.Add(document);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Membership document {DocumentId} attached ({Type}) to application {ApplicationId}",
                document.Id, documentType, applicationId);
            return document.Id;
        }

        public async Task<IReadOnlyList<MembershipDocumentDto>> ListForApplicationAsync(
            Guid applicationId,
            CancellationToken ct = default)
        {
            var documents = await dbContext.MembershipDocuments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(d => d.ApplicationId == applicationId)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync(ct);

            return documents.Select(d => new MembershipDocumentDto(
                Id: d.Id,
                HtxTenantId: d.TenantId.Value,
                ApplicationId: d.ApplicationId,
                DocumentType: d.DocumentType.ToString(),
                DocumentVersion: d.DocumentVersion,
                StorageReference: d.StorageReference,
                Hash: d.Hash,
                CreatedAt: d.CreatedAt)).ToList();
        }
    }
}

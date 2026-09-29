using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.MembershipAggregate;

namespace VanAn.CoreHub.Services.Membership
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): Digital Consent (SRS §14).
    /// Append-only — chỉ thêm bản ghi, không Update/Delete (bảo vệ evidence).
    /// </summary>
    public class ConsentService(
        IVanAnDbContext dbContext,
        ILogger<ConsentService> logger) : IConsentService
    {
        public async Task<Guid> RecordConsentAsync(
            Guid htxTenantId,
            Guid applicantCustomerId,
            ConsentDocumentType documentType,
            string documentVersion,
            string? evidenceReference = null,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(documentVersion);
            if (applicantCustomerId == Guid.Empty)
                throw new ArgumentException("ApplicantCustomerId cannot be empty.", nameof(applicantCustomerId));

            var record = ConsentRecord.Create(
                new TenantId(htxTenantId),
                applicantCustomerId,
                documentType,
                documentVersion,
                evidenceReference);

            dbContext.ConsentRecords.Add(record);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Consent recorded: customer {CustomerId} accepted {DocumentType} v{documentVersion} at HTX {HtxId}",
                applicantCustomerId, documentType, documentVersion, htxTenantId);
            return record.Id;
        }

        public async Task<IReadOnlyList<ConsentDto>> ListForApplicantAsync(
            Guid htxTenantId,
            Guid applicantCustomerId,
            CancellationToken ct = default)
        {
            var htxTenantIdVo = new TenantId(htxTenantId);
            var records = await dbContext.ConsentRecords
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => c.TenantId == htxTenantIdVo && c.ApplicantCustomerId == applicantCustomerId)
                .OrderByDescending(c => c.Timestamp)
                .ToListAsync(ct);

            return records.Select(c => new ConsentDto(
                Id: c.Id,
                HtxTenantId: c.TenantId.Value,
                ApplicantCustomerId: c.ApplicantCustomerId,
                DocumentType: c.DocumentType.ToString(),
                DocumentVersion: c.DocumentVersion,
                Timestamp: c.Timestamp,
                EvidenceReference: c.EvidenceReference)).ToList();
        }
    }
}

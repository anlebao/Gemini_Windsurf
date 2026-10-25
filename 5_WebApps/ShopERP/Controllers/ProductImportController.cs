using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.DTOs;

namespace VanAn.ShopERP.Controllers;

/// <summary>
/// OCR Onboarding S3 (2026-10-25): import danh sách sản phẩm từ menu OCR
/// vào ShopERP SQLite (per-tenant — Option C: Products sống ở ShopERP, không ở Gateway PG).
///
/// Wizard /admin/onboarding gọi endpoint này SAU khi tenant được NATS sync
/// (TenantSyncSubscriber upsert row vào SQLite mirror). Nếu tenant chưa sync
/// → 409 Conflict, UI retry (poll ≤10s).
///
/// Auth: OwnerOnly policy (bao gồm SystemAdmin role) — sysadmin đang onboarding
/// có quyền tạo sản phẩm cho tenant mới (tương đương impersonate).
///
/// MVP constraint: import chạy trên ShopERP instance hiện tại (nơi sysadmin đang
/// thao tác) — tenant phải được gán đúng ShopInstanceId này khi onboard.
/// </summary>
[ApiController]
[Route("api/products/import")]
[Authorize(Policy = "OwnerOnly")]
public class ProductImportController(
    IProductService productService,
    IVanAnDbContext dbContext,
    ILogger<ProductImportController> logger) : ControllerBase
{
    private readonly IProductService _productService = productService;
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly ILogger<ProductImportController> _logger = logger;

    /// <summary>
    /// Import sản phẩm từ menu OCR. Yêu cầu tenant đã tồn tại trong SQLite mirror
    /// (NATS sync xong) — nếu chưa → 409. Mỗi dòng lỗi được bỏ qua riêng (không
    /// làm hỏng toàn bộ batch); kết quả trả Imported/Skipped/Errors.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProductImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProductImportResult>> Import(
        [FromBody] ProductImportRequest request,
        CancellationToken ct)
    {
        if (request.TenantId == Guid.Empty)
            return BadRequest(new { error = "TenantId là bắt buộc." });
        if (request.Lines is null || request.Lines.Count == 0)
            return BadRequest(new { error = "Danh sách sản phẩm trống." });
        if (request.Lines.Count > 500)
            return BadRequest(new { error = "Tối đa 500 sản phẩm mỗi lần import." });

        // Tenant phải đã sync sang SQLite mirror (NATS) — nếu chưa → 409 + UI retry
        var tenantExists = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(t => t.Id == new TenantId(request.TenantId), ct);
        if (!tenantExists)
        {
            _logger.LogInformation("Product import: tenant {TenantId} chưa sync vào SQLite — 409", request.TenantId);
            return Conflict(new { error = "Tenant chưa được đồng bộ — thử lại trong vài giây." });
        }

        var result = new ProductImportResult();
        var category = string.IsNullOrWhiteSpace(request.DefaultCategory) ? "Menu" : request.DefaultCategory.Trim();

        foreach (var line in request.Lines)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(line.Name))
                {
                    result.Skipped++;
                    result.Errors.Add(new ProductImportError { LineNo = line.LineNo, Name = line.Name, Error = "Tên sản phẩm trống." });
                    continue;
                }

                await _productService.CreateProductAsync(new CreateProductRequest
                {
                    Name = line.Name.Trim(),
                    Price = line.Price ?? 0m,
                    Category = category,
                    VatRate = 0.10m
                }, request.TenantId, ct);

                result.Imported++;
            }
            catch (Exception ex)
            {
                result.Skipped++;
                result.Errors.Add(new ProductImportError { LineNo = line.LineNo, Name = line.Name, Error = ex.Message });
                _logger.LogWarning(ex, "Product import skip line {LineNo} ({Name})", line.LineNo, line.Name);
            }
        }

        _logger.LogInformation(
            "Product import cho tenant {TenantId}: imported {Imported}, skipped {Skipped}",
            request.TenantId, result.Imported, result.Skipped);

        return Ok(result);
    }
}
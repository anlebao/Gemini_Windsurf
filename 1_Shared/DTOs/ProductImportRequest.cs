namespace VanAn.Shared.DTOs;

/// <summary>
/// OCR Onboarding S3 (2026-10-25): import danh sách sản phẩm từ menu OCR
/// vào ShopERP SQLite (per-tenant). DTO boundary — DataAnnotations OK.
/// </summary>
public class ProductImportRequest
{
    /// <summary>TenantId của tenant vừa onboard (Pending → Verify → NATS sync).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Danh sách sản phẩm từ menu OCR (review/edit xong trên wizard).</summary>
    public List<ProductImportLine> Lines { get; set; } = [];

    /// <summary>Danh mục mặc định cho sản phẩm OCR (menu không OCR được category).</summary>
    public string DefaultCategory { get; set; } = "Menu";
}

public class ProductImportLine
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Tên sản phẩm là bắt buộc")]
    public string Name { get; set; } = string.Empty;

    public decimal? Price { get; set; }

    public int LineNo { get; set; }
}

public class ProductImportResult
{
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public List<ProductImportError> Errors { get; set; } = [];
}

public class ProductImportError
{
    public int LineNo { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}